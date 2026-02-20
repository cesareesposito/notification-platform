namespace Notification.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>notification_templates</c> table.
/// Unique constraint on (TenantId, TemplateName, Channel, Language).
/// Use TenantId = "default" for fallback templates shared across tenants.
/// </summary>
public class NotificationTemplateEntity
{
    public int Id { get; set; }

    public string TenantId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>Stored as string: "Email", "Push", "WebPush".</summary>
    public string Channel { get; set; } = string.Empty;

    public string Language { get; set; } = "en";

    /// <summary>Raw Scriban template content.</summary>
    public string Content { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Monotonic version counter, incremented on each PUT.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation (optional, not required by EF but useful for queries)
    public TenantEntity? Tenant { get; set; }
}
