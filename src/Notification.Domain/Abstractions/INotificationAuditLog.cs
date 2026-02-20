using Notification.Domain.Models;

namespace Notification.Domain.Abstractions;

/// <summary>
/// Persists send outcomes for audit, analytics, and debugging.
/// </summary>
public interface INotificationAuditLog
{
    Task RecordAsync(
        NotificationMessage message,
        NotificationResult result,
        CancellationToken cancellationToken = default);
}
