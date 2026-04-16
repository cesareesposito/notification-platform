using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Notification.Persistence;

namespace Notification.Api.Middleware;

/// <summary>
/// Validates X-Api-Key header against hashed keys stored in the database.
/// Uses IMemoryCache to avoid a DB round-trip on every request (TTL: 5 minutes).
/// Falls back to appsettings ApiAuth:Keys if the database table is empty (backward compat / dev mode).
/// JWT-authenticated requests (admin UI) bypass the X-Api-Key check entirely.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    private const string CachePrefix = "apikey:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for health checks, auth endpoints, admin SPA static files, and Swagger UI
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health")
            || path.StartsWithSegments("/api/auth")
            || path.StartsWithSegments("/admin")
            || path.StartsWithSegments("/swagger")
            || path.Value == "/")
        {
            await _next(context);
            return;
        }

        // JWT-authenticated requests (admin UI) bypass X-Api-Key check
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
            || string.IsNullOrWhiteSpace(providedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing API key." });
            return;
        }

        var rawKey = providedKey.ToString();
        var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
        var dbFactory = context.RequestServices.GetRequiredService<IDbContextFactory<NotificationDbContext>>();

        var tenant = await ResolveKeyAsync(rawKey, cache, dbFactory);

        if (tenant is null)
        {
            // Fall back to config-based keys (backward compat / dev mode)
            var configKeys = _configuration.GetSection("ApiAuth:Keys").Get<string[]>() ?? [];
            if (configKeys.Length == 0 || !configKeys.Contains(rawKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid or revoked API key." });
                return;
            }
            // Config key valid: set a generic client identity so [Authorize] policies pass
            SetClientIdentity(context, clientName: "config-key", clientId: null);
        }
        else
        {
            SetClientIdentity(context, tenant.DisplayName, tenant.ClientId);
        }

        await _next(context);
    }

    private static void SetClientIdentity(HttpContext context, string clientName, string? clientId)
    {
        var claims = new List<Claim>
        {
            new("scope", "client"),
            new("clientName", clientName),
        };
        if (clientId is not null)
            claims.Add(new Claim("notificationClientId", clientId));

        var identity = new ClaimsIdentity(claims, authenticationType: "ApiKey");
        context.User = new ClaimsPrincipal(identity);
    }

    private static async Task<Notification.Persistence.Entities.TenantEntity?> ResolveKeyAsync(
        string rawKey,
        IMemoryCache cache,
        IDbContextFactory<NotificationDbContext> dbFactory)
    {
        var hash = ComputeSha256(rawKey);
        var cacheKey = CachePrefix + hash;

        if (cache.TryGetValue(cacheKey, out Notification.Persistence.Entities.TenantEntity? cachedTenant))
            return cachedTenant;

        await using var db = await dbFactory.CreateDbContextAsync();
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ApiKeyHash == hash && t.IsActive);

        cache.Set(cacheKey, tenant, CacheTtl);
        return tenant;
    }

    internal static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}

