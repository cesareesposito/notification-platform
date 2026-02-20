using System.ComponentModel.DataAnnotations;
using Notification.Domain.Models;

namespace Notification.Api.Requests;

public class SendBulkRequest
{
    [Required] public string TenantId { get; init; } = string.Empty;

    [Required, MinLength(1), MaxLength(1000)]
    public List<BulkRecipient> Recipients { get; init; } = new();

    [Required] public string TemplateName { get; init; } = string.Empty;
    public NotificationChannel Channel { get; init; } = NotificationChannel.Email;
    public string Language { get; init; } = "en";

    /// <summary>Shared data applied to all recipients (can be overridden per recipient).</summary>
    public Dictionary<string, object?> SharedData { get; init; } = new();
}

public class BulkRecipient
{
    [Required] public string Recipient { get; init; } = string.Empty;
    public string? RecipientName { get; init; }

    /// <summary>Per-recipient data that overrides SharedData.</summary>
    public Dictionary<string, object?> Data { get; init; } = new();
}
