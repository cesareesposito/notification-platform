namespace Notification.Domain.Models;

/// <summary>
/// The message that travels through the queue between API and Worker.
/// Contains the original request plus routing metadata.
/// </summary>
public class NotificationMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public NotificationRequest Request { get; init; } = null!;
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;
    public int AttemptCount { get; set; } = 0;
    public string? CorrelationId { get; init; }
}
