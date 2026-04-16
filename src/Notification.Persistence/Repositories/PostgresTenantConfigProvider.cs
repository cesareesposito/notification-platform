using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;
using Notification.Persistence.Entities;

namespace Notification.Persistence.Repositories;

/// <summary>
/// Reads tenant configuration from PostgreSQL.
/// Results are cached in-process for 5 minutes to avoid a DB round-trip on
/// every notification. The cache entry is evicted when a tenant is updated
/// via <see cref="InvalidateCacheAsync"/>.
/// </summary>
public class PostgresTenantConfigProvider : ITenantConfigProvider
{
    private readonly IDbContextFactory<NotificationDbContext> _factory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PostgresTenantConfigProvider> _logger;

    private static string CacheKey(string clientId) => $"tenant:{clientId}";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public PostgresTenantConfigProvider(
        IDbContextFactory<NotificationDbContext> factory,
        IMemoryCache cache,
        ILogger<PostgresTenantConfigProvider> logger)
    {
        _factory = factory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TenantConfig?> GetConfigAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(clientId);

        if (_cache.TryGetValue(key, out TenantConfig? cached))
            return cached;

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var entity = await db.Tenants
            .AsNoTracking()
            .Where(t => t.ClientId == clientId && t.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            _logger.LogWarning("Client '{ClientId}' not found in database.", clientId);
            return null;
        }

        var config = MapToConfig(entity);

        _cache.Set(key, config, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        return config;
    }

    /// <summary>
    /// Call this after creating or updating a tenant to invalidate the cache.
    /// </summary>
    public void InvalidateCache(string clientId) =>
        _cache.Remove(CacheKey(clientId));

    private static TenantConfig MapToConfig(TenantEntity e) => new()
    {
        ClientId = e.ClientId,
        DisplayName = e.DisplayName,
        EmailProvider = e.EmailProvider,
        EmailFrom = e.EmailFrom,
        EmailFromName = e.EmailFromName,
        PushProvider = e.PushProvider,
        TemplateBasePath = e.TemplateBasePath,
        ProviderSettings = e.ProviderSettings,
        RateLimitPerMinute = e.RateLimitPerMinute
    };
}
