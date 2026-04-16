using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Notification.Persistence;
using Notification.Persistence.Entities;
using System.Text.RegularExpressions;

namespace Notification.Api.Controllers;

/// <summary>
/// CRUD for client API keys.
/// All endpoints require scope:admin JWT.
/// The raw key is returned only on creation and never stored (only SHA-256 hash is persisted).
/// </summary>
[ApiController]
[Route("api/admin/apikeys")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class ApiKeysController : ControllerBase
{
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly IMemoryCache _cache;
    private const string CachePrefix = "apikey:";

    public ApiKeysController(IDbContextFactory<NotificationDbContext> dbFactory, IMemoryCache cache)
    {
        _dbFactory = dbFactory;
        _cache = cache;
    }

    /// <summary>List all API keys (metadata only, no raw values).</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var keys = await db.Tenants
            .AsNoTracking()
            .Where(t => t.ApiKeyHash != null || t.ApiKeyCreatedAt != null || t.ApiKeyRevokedAt != null)
            .OrderBy(t => t.ClientId)
            .ThenBy(t => t.DisplayName)
            .ToListAsync(ct);

        return Ok(keys.Select(ApiKeyResponse));
    }

    /// <summary>
    /// Create or rotate the API key for the given client. If the client does not exist,
    /// it is created transparently with a minimal default configuration.
    /// Returns the raw key once.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var clientName = req.ClientName.Trim();
        var baseClientId = SlugifyClientId(clientName);
        if (string.IsNullOrWhiteSpace(baseClientId))
            return BadRequest(new { error = "ClientName deve contenere almeno un carattere valido." });

        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.ClientId == baseClientId, ct);
        var tenantCreated = false;

        if (entity is null)
        {
            var clientId = await GenerateUniqueClientIdAsync(db, baseClientId, ct);
            entity = CreateTenant(clientId, clientName);
            db.Tenants.Add(entity);
            tenantCreated = true;
        }

        // Generate a cryptographically random key (32 bytes → 64 hex chars)
        var rawKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var keyHash = ComputeSha256(rawKey);
        var previousKeyHash = entity.ApiKeyHash;
        var now = DateTimeOffset.UtcNow;

        entity.DisplayName = clientName;
        entity.ApiKeyHash = keyHash;
        entity.ApiKeyCreatedAt = now;
        entity.ApiKeyRevokedAt = null;
        entity.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(previousKeyHash) && !string.Equals(previousKeyHash, keyHash, StringComparison.Ordinal))
            _cache.Remove(CachePrefix + previousKeyHash);

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            id = entity.Id,
            clientName = entity.DisplayName,
            clientId = entity.ClientId,
            createdAt = entity.ApiKeyCreatedAt,
            tenantCreated,
            rawKey  // shown ONCE — not stored
        });
    }

    /// <summary>Revoke a client API key.</summary>
    [HttpDelete("{clientId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(string clientId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.ClientId == clientId, ct);
        if (entity is null) return NotFound();

        var previousKeyHash = entity.ApiKeyHash;
        if (string.IsNullOrWhiteSpace(previousKeyHash) && entity.ApiKeyCreatedAt is null)
            return NotFound();

        entity.ApiKeyHash = null;
        entity.ApiKeyRevokedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Invalidate cache entry so the key is rejected immediately
        if (!string.IsNullOrWhiteSpace(previousKeyHash))
            _cache.Remove(CachePrefix + previousKeyHash);

        return NoContent();
    }

    private static object ApiKeyResponse(TenantEntity tenant) => new
    {
        Id = tenant.Id,
        ClientName = tenant.DisplayName,
        ClientId = tenant.ClientId,
        IsActive = !string.IsNullOrWhiteSpace(tenant.ApiKeyHash),
        CreatedAt = tenant.ApiKeyCreatedAt ?? tenant.CreatedAt,
        RevokedAt = tenant.ApiKeyRevokedAt
    };

    private static TenantEntity CreateTenant(string clientId, string clientName) => new()
    {
        ClientId = clientId,
        DisplayName = clientName,
        EmailProvider = "Smtp",
        PushProvider = "Firebase",
        TemplateBasePath = string.Empty,
        ProviderSettings = new(),
        RateLimitPerMinute = 100,
        IsActive = true
    };

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private static async Task<string> GenerateUniqueClientIdAsync(
        NotificationDbContext db,
        string baseClientId,
        CancellationToken ct)
    {
        var candidate = baseClientId;
        var suffix = 2;

        while (await db.Tenants.AnyAsync(t => t.ClientId == candidate, ct))
        {
            candidate = $"{baseClientId}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string SlugifyClientId(string input)
    {
        var normalized = Regex.Replace(input.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-");
        return normalized.Trim('-');
    }
}

public class CreateApiKeyRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string ClientName { get; set; } = string.Empty;
}
