using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.Api.Requests.Admin;
using Notification.Persistence;
using Notification.Persistence.Entities;
using Notification.Persistence.Repositories;

namespace Notification.Api.Controllers;

/// <summary>
/// CRUD management for tenants and notification templates.
/// Requires either a valid X-Api-Key header (enforced by ApiKeyAuthMiddleware)
/// or a JWT with scope:admin or scope:client (issued by AuthController).
/// scope:client requests are automatically scoped to their notificationClientId claim.
/// scope:admin requests have unrestricted access.
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
[Authorize(Policy = "AnyAuth")]
public class AdminController : ControllerBase
{
    // Must match SmtpEmailProvider.DataProtectionPurpose so both encrypt/decrypt with the same key.
    private const string SmtpDataProtectionPurpose = "SmtpPassword";

    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly PostgresTenantConfigProvider _tenantProvider;
    private readonly PostgresTemplateRepository _templateRepository;
    private readonly IDataProtector _protector;

    public AdminController(
        IDbContextFactory<NotificationDbContext> dbFactory,
        PostgresTenantConfigProvider tenantProvider,
        PostgresTemplateRepository templateRepository,
        IDataProtectionProvider dataProtectionProvider)
    {
        _dbFactory = dbFactory;
        _tenantProvider = tenantProvider;
        _templateRepository = templateRepository;
        _protector = dataProtectionProvider.CreateProtector(SmtpDataProtectionPurpose);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TENANTS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>List all clients. scope:client sees only its own record.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        IQueryable<TenantEntity> query = db.Tenants.AsNoTracking().OrderBy(t => t.ClientId);

        if (!IsAdminScope())
        {
            var clientId = GetClientId();
            if (clientId is null) return Forbid();
            query = query.Where(t => t.ClientId == clientId);
        }

        var tenants = await query.ToListAsync(ct);
        return Ok(tenants.Select(TenantResponse));
    }

    /// <summary>Get a single client by ID. scope:client can only access its own record.</summary>
    [HttpGet("tenants/{clientId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(string clientId, CancellationToken ct)
    {
        var effectiveClientId = ResolveClientId(clientId);
        if (effectiveClientId is null) return Forbid();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.ClientId == effectiveClientId, ct);
        return tenant is null ? NotFound() : Ok(TenantResponse(tenant));
    }

    /// <summary>Create a new client. Requires scope:admin.</summary>
    [HttpPost("tenants/{clientId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTenant(
        string clientId,
        [FromBody] UpsertTenantRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        if (await db.Tenants.AnyAsync(t => t.ClientId == clientId, ct))
            return Conflict(new { error = $"Client '{clientId}' already exists." });

        var entity = new TenantEntity
        {
            ClientId = clientId,
            DisplayName = req.DisplayName.Trim(),
            EmailProvider = req.EmailProvider,
            EmailFrom = req.EmailFrom,
            EmailFromName = req.EmailFromName,
            PushProvider = req.PushProvider,
            ProviderSettings = EncryptSmtpPassword(req.ProviderSettings),
            RateLimitPerMinute = req.RateLimitPerMinute,
            IsActive = req.IsActive
        };

        db.Tenants.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTenant), new { clientId }, TenantResponse(entity));
    }

    /// <summary>Update an existing client. scope:client can only update its own record.</summary>
    [HttpPut("tenants/{clientId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenant(
        string clientId,
        [FromBody] UpsertTenantRequest req,
        CancellationToken ct)
    {
        var effectiveClientId = ResolveClientId(clientId);
        if (effectiveClientId is null) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.ClientId == effectiveClientId, ct);
        if (entity is null) return NotFound();

        entity.DisplayName = req.DisplayName.Trim();
        entity.EmailProvider = req.EmailProvider;
        entity.EmailFrom = req.EmailFrom;
        entity.EmailFromName = req.EmailFromName;
        entity.PushProvider = req.PushProvider;
        // Pass existing encrypted settings so a blank/placeholder password keeps the current value.
        entity.ProviderSettings = EncryptSmtpPassword(req.ProviderSettings, entity.ProviderSettings);
        entity.RateLimitPerMinute = req.RateLimitPerMinute;
        entity.IsActive = req.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        _tenantProvider.InvalidateCache(effectiveClientId);
        return Ok(TenantResponse(entity));
    }

    /// <summary>Delete a client (and its templates via cascade). Requires scope:admin.</summary>
    [HttpDelete("tenants/{clientId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTenant(string clientId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.ClientId == clientId, ct);
        if (entity is null) return NotFound();

        db.Tenants.Remove(entity);
        await db.SaveChangesAsync(ct);

        _tenantProvider.InvalidateCache(clientId);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEMPLATES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>List all templates for a client. scope:client can only list its own templates.</summary>
    [HttpGet("tenants/{clientId}/templates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(string clientId, CancellationToken ct)
    {
        var effectiveClientId = ResolveClientId(clientId);
        if (effectiveClientId is null) return Forbid();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var templates = await db.Templates.AsNoTracking()
            .Where(t => t.ClientId == effectiveClientId || t.ClientId == "default")
            .OrderBy(t => t.TemplateName).ThenBy(t => t.Channel).ThenBy(t => t.Language)
            .ToListAsync(ct);

        var mergedTemplates = templates
            .GroupBy(t => $"{t.TemplateName}||{t.Channel}||{t.Language}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(t => string.Equals(t.ClientId, effectiveClientId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(t => t.Version)
                .First())
            .Select(TemplateResponse);

        return Ok(mergedTemplates);
    }

    /// <summary>Create or update a template for a client. scope:client can only manage its own templates.</summary>
    [HttpPost("tenants/{clientId}/templates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpsertTemplate(
        string clientId,
        [FromBody] UpsertTemplateRequest req,
        CancellationToken ct)
    {
        var effectiveClientId = ResolveClientId(clientId);
        if (effectiveClientId is null) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem();

        var channel = req.Channel.ToString();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.Templates.FirstOrDefaultAsync(
                        t => t.ClientId == effectiveClientId
              && t.TemplateName == req.TemplateName
              && t.Channel == channel
              && t.Language == req.Language, ct);

        if (existing is not null)
        {
            existing.Content = req.Content;
            existing.IsActive = req.IsActive;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.Version++;
        }
        else
        {
            existing = new NotificationTemplateEntity
            {
                ClientId = effectiveClientId,
                TemplateName = req.TemplateName,
                Channel = channel,
                Language = req.Language,
                Content = req.Content,
                IsActive = req.IsActive
            };
            db.Templates.Add(existing);
        }

        await db.SaveChangesAsync(ct);

        // Invalidate cache for all fallback keys that might have resolved this template
    var key = new Domain.Models.NotificationTemplateKey(effectiveClientId, req.TemplateName, req.Channel, req.Language);
        _templateRepository.InvalidateCache(key);

        return Ok(TemplateResponse(existing));
    }

    /// <summary>Get a template by its numeric ID, including full content.</summary>
    [HttpGet("templates/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplate(int id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is not null && !CanReadTemplate(entity))
            return Forbid();
        return entity is null ? NotFound() : Ok(TemplateDetailResponse(entity));
    }

    /// <summary>Update a template by its numeric ID.</summary>
    [HttpPut("templates/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTemplate(
        int id,
        [FromBody] UpsertTemplateRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Templates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFound();
        if (!CanMutateTemplate(entity)) return Forbid();

        entity.Content = req.Content;
        entity.IsActive = req.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version++;

        await db.SaveChangesAsync(ct);

        var key = new Domain.Models.NotificationTemplateKey(
            entity.ClientId, entity.TemplateName, req.Channel, entity.Language);
        _templateRepository.InvalidateCache(key);

        return Ok(TemplateResponse(entity));
    }

    /// <summary>Delete a template by its numeric ID.</summary>
    [HttpDelete("templates/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Templates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFound();
        if (!CanMutateTemplate(entity)) return Forbid();

        db.Templates.Remove(entity);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── Scope helpers ─────────────────────────────────────────────────────────

    private bool IsAdminScope() =>
        User.HasClaim("scope", "admin");

    private string? GetClientId() =>
        User.FindFirst("notificationClientId")?.Value;

    private string? ResolveClientId(string? requestedClientId)
    {
        if (IsAdminScope())
            return requestedClientId;

        return GetClientId();
    }

    private bool CanReadTemplate(NotificationTemplateEntity entity)
    {
        if (IsAdminScope())
            return true;

        var clientId = GetClientId();
        if (clientId is null)
            return false;

        return string.Equals(entity.ClientId, clientId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.ClientId, "default", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanMutateTemplate(NotificationTemplateEntity entity)
    {
        if (IsAdminScope())
            return true;

        var clientId = GetClientId();
        if (clientId is null)
            return false;

        return !string.Equals(entity.ClientId, "default", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entity.ClientId, clientId, StringComparison.OrdinalIgnoreCase);
    }

    // ── Response projections ──────────────────────────────────────────────────

    private static object TenantResponse(TenantEntity t) => new
    {
        t.Id,
        t.ClientId,
        t.DisplayName,
        t.EmailProvider,
        t.EmailFrom,
        t.EmailFromName,
        t.PushProvider,
        ProviderSettings = MaskSensitiveSettings(t.ProviderSettings),
        t.RateLimitPerMinute,
        t.IsActive,
        t.CreatedAt,
        t.UpdatedAt
    };

    // ── Sensitive settings helpers ────────────────────────────────────────────

    /// <summary>
    /// Returns a copy of provider settings with sensitive values replaced by "***".
    /// Never expose encrypted ciphertext or plaintext secrets in API responses.
    /// </summary>
    private static Dictionary<string, string> MaskSensitiveSettings(Dictionary<string, string> settings)
    {
        if (!settings.ContainsKey("Smtp:Password")) return settings;
        var masked = new Dictionary<string, string>(settings) { ["Smtp:Password"] = "***" };
        return masked;
    }

    /// <summary>
    /// Encrypts Smtp:Password before persisting.
    /// On update, if the caller sends "" or "***" (placeholder), the existing encrypted value is kept.
    /// </summary>
    private Dictionary<string, string> EncryptSmtpPassword(
        Dictionary<string, string> incoming,
        Dictionary<string, string>? existing = null)
    {
        var result = new Dictionary<string, string>(incoming);

        if (!result.TryGetValue("Smtp:Password", out var pwd))
            return result;

        if (string.IsNullOrEmpty(pwd) || pwd == "***")
        {
            // Preserve the already-encrypted password stored in DB.
            if (existing?.TryGetValue("Smtp:Password", out var kept) == true)
                result["Smtp:Password"] = kept;
            else
                result.Remove("Smtp:Password");
        }
        else
        {
            result["Smtp:Password"] = _protector.Protect(pwd);
        }

        return result;
    }

    private static object TemplateResponse(NotificationTemplateEntity t) => new
    {
        t.Id,
        t.ClientId,
        t.TemplateName,
        t.Channel,
        t.Language,
        t.IsActive,
        t.Version,
        t.CreatedAt,
        t.UpdatedAt,
        ContentPreview = t.Content.Length > 200 ? t.Content[..200] + "…" : t.Content
    };

    private static object TemplateDetailResponse(NotificationTemplateEntity t) => new
    {
        t.Id,
        t.ClientId,
        t.TemplateName,
        t.Channel,
        t.Language,
        t.Content,
        t.IsActive,
        t.Version,
        t.CreatedAt,
        t.UpdatedAt
    };
}
