using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notification.Persistence.Entities;

namespace Notification.Persistence.Seeding;

/// <summary>
/// Runs at application startup to:
/// 1. Apply pending EF Core migrations (auto-migrate).
/// 2. Seed the initial tenants from <see cref="SeedTenants"/> if they don't exist.
/// 3. Import *.scriban files from the filesystem templates directory (idempotent upsert).
/// </summary>
public class DatabaseSeeder
{
    private const long SeederAdvisoryLockId = 681294731145923516L;
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseSeeder(
        IDbContextFactory<NotificationDbContext> dbFactory,
        ILogger<DatabaseSeeder> logger,
        IConfiguration configuration)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SeedAsync(string templatesBasePath = "templates", CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock({SeederAdvisoryLockId})", ct);

            // 1. Auto-migrate
            _logger.LogInformation("Applying pending database migrations…");
            await db.Database.MigrateAsync(ct);

            // 2. Seed tenants
            await SeedTenantsAsync(db, ct);

            // 3. Seed admin users
            await SeedAdminUsersAsync(db, ct);

            // 4. Import filesystem templates
            await ImportFilesystemTemplatesAsync(db, templatesBasePath, ct);

            _logger.LogInformation("Database seeding completed.");
        }
        finally
        {
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_unlock({SeederAdvisoryLockId})", ct);
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    // ── Tenants ───────────────────────────────────────────────────────────────

    private async Task SeedTenantsAsync(NotificationDbContext db, CancellationToken ct)
    {
        foreach (var seed in SeedTenants)
        {
            var exists = await db.Tenants.AnyAsync(t => t.ClientId == seed.ClientId, ct);
            if (exists)
            {
                _logger.LogDebug("Client '{ClientId}' already exists, skipping.", seed.ClientId);
                continue;
            }

            db.Tenants.Add(seed.CreateEntity());
            _logger.LogInformation("Seeded client '{ClientId}'.", seed.ClientId);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Override or replace with data from configuration / environment as needed.
    /// </summary>
    private static readonly IReadOnlyList<TenantSeed> SeedTenants =
    [
        // "default" is the virtual fallback tenant — templates here are shared by all tenants
        new()
        {
            ClientId = "default",
            DisplayName = "Default (fallback templates)",
            EmailProvider = "Smtp",
            PushProvider = "Firebase",
            IsActive = true
        }
    ];

    private sealed class TenantSeed
    {
        public required string ClientId { get; init; }
        public required string DisplayName { get; init; }
        public required string EmailProvider { get; init; }
        public string? EmailFrom { get; init; }
        public string? EmailFromName { get; init; }
        public required string PushProvider { get; init; }
        public Dictionary<string, string> ProviderSettings { get; init; } = [];
        public int RateLimitPerMinute { get; init; } = 100;
        public bool IsActive { get; init; } = true;

        public TenantEntity CreateEntity() => new()
        {
            ClientId = ClientId,
            DisplayName = DisplayName,
            EmailProvider = EmailProvider,
            EmailFrom = EmailFrom,
            EmailFromName = EmailFromName,
            PushProvider = PushProvider,
            ProviderSettings = new Dictionary<string, string>(ProviderSettings),
            RateLimitPerMinute = RateLimitPerMinute,
            IsActive = IsActive
        };
    }

    // ── Admin Users ───────────────────────────────────────────────────────────

    private async Task SeedAdminUsersAsync(NotificationDbContext db, CancellationToken ct)
    {
        if (await db.AdminUsers.AnyAsync(ct))
        {
            _logger.LogDebug("Admin users already seeded, skipping.");
            return;
        }

        var defaultPassword = _configuration["AdminAuth:DefaultPassword"] ?? "changeme";
        var hasher = new PasswordHasher<AdminUserEntity>();
        var admin = new AdminUserEntity
        {
            Username = "admin",
            Role = "admin",
            IsActive = true
        };
        admin.PasswordHash = hasher.HashPassword(admin, defaultPassword);

        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded admin user 'admin'.");
    }

    // ── Filesystem template import ────────────────────────────────────────────

    /// <summary>
    /// Walks <paramref name="basePath"/> looking for files matching:
    ///   {clientId}/{channel}/{language}/{templateName}.scriban
    /// and upserts them into notification_templates.
    /// </summary>
    private async Task ImportFilesystemTemplatesAsync(NotificationDbContext db, string basePath, CancellationToken ct)
    {
        if (!Directory.Exists(basePath))
        {
            _logger.LogWarning("Templates directory '{Path}' not found, skipping import.", basePath);
            return;
        }

        var scribanFiles = Directory.GetFiles(basePath, "*.scriban", SearchOption.AllDirectories);

        foreach (var file in scribanFiles)
        {
            var relative = Path.GetRelativePath(basePath, file);
            var parts = relative.Split(Path.DirectorySeparatorChar);

            // Expected structure: clientId / channel / language / templateName.scriban
            if (parts.Length != 4)
            {
                _logger.LogWarning("Skipping template file with unexpected path: {File}", relative);
                continue;
            }

            var clientId = parts[0];
            var channelRaw = parts[1];
            var language = parts[2];
            var templateName = Path.GetFileNameWithoutExtension(parts[3]);

            // Normalize channel to match domain enum casing
            var channel = channelRaw switch
            {
                "email" => "Email",
                "push" => "Push",
                "webpush" => "WebPush",
                _ => channelRaw
            };

            var content = await File.ReadAllTextAsync(file, ct);

            // Upsert: update if exists, insert if not
                        var existing = await db.Templates.FirstOrDefaultAsync(
                    t => t.ClientId == clientId
                  && t.TemplateName == templateName
                  && t.Channel == channel
                  && t.Language == language, ct);

            if (existing is not null)
            {
                existing.Content = content;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                existing.Version++;
            }
            else
            {
                db.Templates.Add(new NotificationTemplateEntity
                {
                    ClientId = clientId,
                    TemplateName = templateName,
                    Channel = channel,
                    Language = language,
                    Content = content
                });
                _logger.LogInformation(
                    "Imported template '{Name}' for client='{ClientId}' channel={Channel} lang={Lang}",
                    templateName, clientId, channel, language);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
