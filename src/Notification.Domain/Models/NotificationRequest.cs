namespace Notification.Domain.Models;

/// <summary>
/// Inbound payload accepted by the API.
/// </summary>
public class NotificationRequest
{
    /// <summary>Tenant that owns this notification.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Delivery channel.</summary>
    public NotificationChannel Channel { get; init; }

    /// <summary>Recipient: email address, device token or push subscription.</summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>Optional recipient name (used in email templates).</summary>
    public string? RecipientName { get; init; }

    /// <summary>Name of the template to render (e.g. "welcome", "order-confirmed").</summary>
    public string TemplateName { get; init; } = string.Empty;

    /// <summary>Language / locale code for template selection.</summary>
    public string Language { get; init; } = "en";

    /// <summary>Key/value data injected into the template.</summary>
    public Dictionary<string, object?> Data { get; init; } = new();

    /// <summary>Optional idempotency key to avoid duplicate sends.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Optional scheduled delivery time (UTC). Null = send immediately.</summary>
    public DateTimeOffset? ScheduledAt { get; init; }
}
