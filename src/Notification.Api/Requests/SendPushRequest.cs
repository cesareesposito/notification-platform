using System.ComponentModel.DataAnnotations;

namespace Notification.Api.Requests;

public class SendPushRequest
{
    [Required] public string ClientId { get; init; } = string.Empty;

    /// <summary>FCM/APNs device token or WebPush endpoint.</summary>
    [Required] public string DeviceToken { get; init; } = string.Empty;

    public string? RecipientName { get; init; }
    [Required] public string TemplateName { get; init; } = string.Empty;
    public string Language { get; init; } = "en";
    public Dictionary<string, object?> Data { get; init; } = new();
    public string? IdempotencyKey { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
}
