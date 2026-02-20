using System.ComponentModel.DataAnnotations;

namespace Notification.Api.Requests;

public class SendEmailRequest
{
    [Required] public string TenantId { get; init; } = string.Empty;
    [Required, EmailAddress] public string Recipient { get; init; } = string.Empty;
    public string? RecipientName { get; init; }
    [Required] public string TemplateName { get; init; } = string.Empty;
    public string Language { get; init; } = "en";
    public Dictionary<string, object?> Data { get; init; } = new();
    public string? IdempotencyKey { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
}
