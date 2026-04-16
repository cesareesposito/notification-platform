using Microsoft.EntityFrameworkCore;
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
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(IDbContextFactory<NotificationDbContext> dbFactory, ILogger<DatabaseSeeder> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task SeedAsync(string templatesBasePath = "templates", CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // 1. Auto-migrate
        _logger.LogInformation("Applying pending database migrations…");
        await db.Database.MigrateAsync(ct);

        // 2. Seed tenants
        await SeedTenantsAsync(db, ct);

        // 3. Import filesystem templates
        await ImportFilesystemTemplatesAsync(db, templatesBasePath, ct);

        _logger.LogInformation("Database seeding completed.");
    }

    // ── Tenants ───────────────────────────────────────────────────────────────

    private async Task SeedTenantsAsync(NotificationDbContext db, CancellationToken ct)
    {
        foreach (var seed in SeedTenants)
        {
            var exists = await db.Tenants.AnyAsync(t => t.TenantId == seed.TenantId, ct);
            if (exists)
            {
                _logger.LogDebug("Tenant '{TenantId}' already exists, skipping.", seed.TenantId);
                continue;
            }

            db.Tenants.Add(seed);
            _logger.LogInformation("Seeded tenant '{TenantId}'.", seed.TenantId);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Override or replace with data from configuration / environment as needed.
    /// </summary>
    private static readonly IReadOnlyList<TenantEntity> SeedTenants =
    [
        // "default" is the virtual fallback tenant — templates here are shared by all tenants
        new()
        {
            TenantId = "default",
            DisplayName = "Default (fallback templates)",
            EmailProvider = "Smtp",
            PushProvider = "Firebase",
            IsActive = true
        },
        new()
        {
            TenantId = "tenant-a",
            DisplayName = "Tenant A",
            EmailProvider = "Smtp",
            EmailFrom = "noreply@tenant-a.com",
            EmailFromName = "Tenant A",
            PushProvider = "Firebase",
            ProviderSettings = new Dictionary<string, string>
            {
                // Per-tenant SMTP overrides can be set via the admin API (POST /admin/tenants/tenant-a):
                // Smtp:Host, Smtp:Port, Smtp:Username, Smtp:Password (encrypted on write).
                // Leave empty to fall back to global Providers:Smtp settings.
                ["Firebase:CredentialFile"] = "/credentials/tenant-a-firebase.json"
            },
            RateLimitPerMinute = 100
        },
        new()
        {
            TenantId = "tenant-b",
            DisplayName = "Tenant B",
            EmailProvider = "Smtp",
            EmailFrom = "noreply@tenant-b.com",
            EmailFromName = "Tenant B",
            PushProvider = "Firebase",
            ProviderSettings = new Dictionary<string, string>
            {
                ["Firebase:CredentialFile"] = "/credentials/tenant-b-firebase.json"
            },
            RateLimitPerMinute = 200
        }
    ];

    // ── Filesystem template import ────────────────────────────────────────────

    /// <summary>
    /// Walks <paramref name="basePath"/> looking for files matching:
    ///   {tenantId}/{channel}/{language}/{templateName}.scriban
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

            // Expected structure: tenantId / channel / language / templateName.scriban
            if (parts.Length != 4)
            {
                _logger.LogWarning("Skipping template file with unexpected path: {File}", relative);
                continue;
            }

            var tenantId = parts[0];
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
                t => t.TenantId == tenantId
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
                    TenantId = tenantId,
                    TemplateName = templateName,
                    Channel = channel,
                    Language = language,
                    Content = content
                });
                _logger.LogInformation(
                    "Imported template '{Name}' for tenant='{TenantId}' channel={Channel} lang={Lang}",
                    templateName, tenantId, channel, language);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
