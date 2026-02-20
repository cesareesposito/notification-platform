using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;

namespace Notification.Persistence.Repositories;

/// <summary>
/// Loads Scriban template content from PostgreSQL with the same fallback chain
/// as the old FileSystemTemplateRepository:
///   1. (tenantId, name, channel, language) exact
///   2. (tenantId, name, channel, "en")      language fallback
///   3. ("default", name, channel, language)  tenant fallback
///   4. ("default", name, channel, "en")
///
/// Results are cached with a 10-minute sliding expiration.
/// </summary>
public class PostgresTemplateRepository : ITemplateRepository
{
    private readonly IDbContextFactory<NotificationDbContext> _factory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PostgresTemplateRepository> _logger;

    private static string CacheKey(NotificationTemplateKey k) =>
        $"template:{k.TenantId}:{k.Channel}:{k.Language}:{k.TemplateName}";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public PostgresTemplateRepository(
        IDbContextFactory<NotificationDbContext> factory,
        IMemoryCache cache,
        ILogger<PostgresTemplateRepository> logger)
    {
        _factory = factory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetTemplateAsync(
        NotificationTemplateKey key,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(key);
        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var content = await LoadWithFallbackAsync(key, cancellationToken);

        if (content is null)
        {
            _logger.LogWarning(
                "Template not found: Tenant={TenantId} Name={Name} Channel={Channel} Lang={Language}",
                key.TenantId, key.TemplateName, key.Channel, key.Language);
            return null;
        }

        _cache.Set(cacheKey, content, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        return content;
    }

    /// <summary>
    /// Call after upsert to invalidate the cache for a specific template.
    /// </summary>
    public void InvalidateCache(NotificationTemplateKey key) =>
        _cache.Remove(CacheKey(key));

    private async Task<string?> LoadWithFallbackAsync(
        NotificationTemplateKey key,
        CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var channel = key.Channel.ToString();
        var tenantIds = new[] { key.TenantId, "default" };

        // Single query: fetch all matching rows for tenant + "default"
        var candidates = await db.Templates
            .AsNoTracking()
            .Where(t =>
                tenantIds.Contains(t.TenantId) &&
                t.TemplateName == key.TemplateName &&
                t.Channel == channel &&
                t.IsActive)
            .ToListAsync(ct);

        // Priority order matching FileSystemTemplateRepository fallback
        var priority = new[]
        {
            (key.TenantId, key.Language),
            (key.TenantId, "en"),
            ("default",    key.Language),
            ("default",    "en")
        };

        foreach (var (tid, lang) in priority)
        {
            var match = candidates.FirstOrDefault(
                c => c.TenantId == tid && c.Language == lang);

            if (match is not null)
            {
                _logger.LogDebug(
                    "Template '{Name}' matched via ({TenantId}, {Lang})",
                    key.TemplateName, tid, lang);
                return match.Content;
            }
        }

        return null;
    }
}
