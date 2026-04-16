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
/// All endpoints require a valid X-Api-Key header (enforced by ApiKeyAuthMiddleware).
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
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

    /// <summary>List all tenants.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenants = await db.Tenants.AsNoTracking().OrderBy(t => t.TenantId).ToListAsync(ct);
        return Ok(tenants.Select(TenantResponse));
    }

    /// <summary>Get a single tenant by ID.</summary>
    [HttpGet("tenants/{tenantId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(string tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        return tenant is null ? NotFound() : Ok(TenantResponse(tenant));
    }

    /// <summary>Create a new tenant.</summary>
    [HttpPost("tenants/{tenantId}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTenant(
        string tenantId,
        [FromBody] UpsertTenantRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        if (await db.Tenants.AnyAsync(t => t.TenantId == tenantId, ct))
            return Conflict(new { error = $"Tenant '{tenantId}' already exists." });

        var entity = new TenantEntity
        {
            TenantId = tenantId,
            DisplayName = req.DisplayName,
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

        return CreatedAtAction(nameof(GetTenant), new { tenantId }, TenantResponse(entity));
    }

    /// <summary>Update an existing tenant.</summary>
    [HttpPut("tenants/{tenantId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenant(
        string tenantId,
        [FromBody] UpsertTenantRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        if (entity is null) return NotFound();

        entity.DisplayName = req.DisplayName;
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

        _tenantProvider.InvalidateCache(tenantId);
        return Ok(TenantResponse(entity));
    }

    /// <summary>Delete a tenant (and its templates via cascade).</summary>
    [HttpDelete("tenants/{tenantId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTenant(string tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        if (entity is null) return NotFound();

        db.Tenants.Remove(entity);
        await db.SaveChangesAsync(ct);

        _tenantProvider.InvalidateCache(tenantId);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEMPLATES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>List all templates for a tenant.</summary>
    [HttpGet("tenants/{tenantId}/templates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(string tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var templates = await db.Templates.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.TemplateName).ThenBy(t => t.Channel).ThenBy(t => t.Language)
            .ToListAsync(ct);
        return Ok(templates.Select(TemplateResponse));
    }

    /// <summary>Create or update a template for a tenant (upsert by name+channel+language).</summary>
    [HttpPost("tenants/{tenantId}/templates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertTemplate(
        string tenantId,
        [FromBody] UpsertTemplateRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        var channel = req.Channel.ToString();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.Templates.FirstOrDefaultAsync(
            t => t.TenantId == tenantId
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
                TenantId = tenantId,
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
        var key = new Domain.Models.NotificationTemplateKey(tenantId, req.TemplateName, req.Channel, req.Language);
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

        entity.Content = req.Content;
        entity.IsActive = req.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version++;

        await db.SaveChangesAsync(ct);

        var key = new Domain.Models.NotificationTemplateKey(
            entity.TenantId, entity.TemplateName, req.Channel, entity.Language);
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

        db.Templates.Remove(entity);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── Response projections ──────────────────────────────────────────────────

    private static object TenantResponse(TenantEntity t) => new
    {
        t.TenantId,
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
        t.TenantId,
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
        t.TenantId,
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
