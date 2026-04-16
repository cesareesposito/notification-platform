namespace Notification.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>tenants</c> table.
/// ProviderSettings is stored as a JSONB column.
/// </summary>
public class TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public string EmailProvider { get; set; } = "smtp";
    public string? EmailFrom { get; set; }
    public string? EmailFromName { get; set; }

    public string PushProvider { get; set; } = "Firebase";

    // Kept for compatibility, not strictly needed when templates are in DB
    public string TemplateBasePath { get; set; } = string.Empty;

    /// <summary>Stored as JSONB in PostgreSQL.</summary>
    public Dictionary<string, string> ProviderSettings { get; set; } = new();

    public int RateLimitPerMinute { get; set; } = 100;
    public bool IsActive { get; set; } = true;

    /// <summary>SHA-256 hex hash of the issued API key. Null when no active key exists.</summary>
    public string? ApiKeyHash { get; set; }
    public DateTimeOffset? ApiKeyCreatedAt { get; set; }
    public DateTimeOffset? ApiKeyRevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
