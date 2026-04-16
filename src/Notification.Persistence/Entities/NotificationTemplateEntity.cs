namespace Notification.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>notification_templates</c> table.
/// Unique constraint on (ClientId, TemplateName, Channel, Language).
/// Use ClientId = "default" for fallback templates shared across clients.
/// </summary>
public class NotificationTemplateEntity
{
    public int Id { get; set; }

    public string ClientId { get; set; } = string.Empty;
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
}
