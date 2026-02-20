using Notification.Domain.Abstractions;

namespace Notification.Worker.Workers;

/// <summary>
/// Long-running hosted service that starts the RabbitMQ consumer
/// and delegates each message to <see cref="NotificationDispatcher"/>.
/// </summary>
public class NotificationWorker : BackgroundService
{
    private readonly INotificationQueueConsumer _consumer;
    private readonly NotificationDispatcher _dispatcher;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(
        INotificationQueueConsumer consumer,
        NotificationDispatcher dispatcher,
        ILogger<NotificationWorker> logger)
    {
        _consumer = consumer;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationWorker starting.");

        await _consumer.StartConsumingAsync(
            handler: (message, ct) => _dispatcher.DispatchAsync(message, ct),
            cancellationToken: stoppingToken);

        _logger.LogInformation("NotificationWorker stopped.");
    }
}
