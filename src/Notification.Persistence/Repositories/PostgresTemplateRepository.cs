using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;

namespace Notification.Persistence.Repositories;

/// <summary>
/// Loads Scriban template content from PostgreSQL with the same fallback chain
/// as the old FileSystemTemplateRepository:
///   1. (clientId, name, channel, language) exact
///   2. (clientId, name, channel, "en")      language fallback
///   3. ("default", name, channel, language) client fallback
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
        $"template:{k.ClientId}:{k.Channel}:{k.Language}:{k.TemplateName}";

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
                "Template not found: ClientId={ClientId} Name={Name} Channel={Channel} Lang={Language}",
                key.ClientId, key.TemplateName, key.Channel, key.Language);
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
        var clientIds = new[] { key.ClientId, "default" };

        // Single query: fetch all matching rows for tenant + "default"
        var candidates = await db.Templates
            .AsNoTracking()
            .Where(t =>
                clientIds.Contains(t.ClientId) &&
                t.TemplateName == key.TemplateName &&
                t.Channel == channel &&
                t.IsActive)
            .ToListAsync(ct);

        // Priority order matching FileSystemTemplateRepository fallback
        var priority = new[]
        {
            (key.ClientId, key.Language),
            (key.ClientId, "en"),
            ("default",    key.Language),
            ("default",    "en")
        };

        foreach (var (tid, lang) in priority)
        {
            var match = candidates.FirstOrDefault(
                c => c.ClientId == tid && c.Language == lang);

            if (match is not null)
            {
                _logger.LogDebug(
                    "Template '{Name}' matched via ({ClientId}, {Lang})",
                    key.TemplateName, tid, lang);
                return match.Content;
            }
        }

        return null;
    }
}
