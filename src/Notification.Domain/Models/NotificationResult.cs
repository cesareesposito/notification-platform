namespace Notification.Domain.Models;

/// <summary>
/// Outcome of a send attempt recorded by the Worker.
/// </summary>
public class NotificationResult
{
    public Guid MessageId { get; init; }
    public NotificationStatus Status { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
    public int AttemptCount { get; init; }

    public static NotificationResult Success(Guid messageId, string? providerMessageId, int attempt) =>
        new() { MessageId = messageId, Status = NotificationStatus.Sent, ProviderMessageId = providerMessageId, AttemptCount = attempt };

    public static NotificationResult Failure(Guid messageId, string error, int attempt) =>
        new() { MessageId = messageId, Status = NotificationStatus.Failed, ErrorMessage = error, AttemptCount = attempt };
}
