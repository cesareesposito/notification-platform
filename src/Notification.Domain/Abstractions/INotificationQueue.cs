using Notification.Domain.Models;

namespace Notification.Domain.Abstractions;

/// <summary>
/// Abstraction over the underlying message broker (RabbitMQ, Pub/Sub, SQS, …).
/// The API publishes; the Worker subscribes.
/// </summary>
public interface INotificationQueue
{
    Task PublishAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Consumer side: receives messages and passes them to a handler.
/// </summary>
public interface INotificationQueueConsumer
{
    Task StartConsumingAsync(
        Func<NotificationMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);
}
