using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Notification.Infrastructure.Queuing;

/// <summary>
/// Subscribes to RabbitMQ and drives the Worker handler per message.
/// Implements manual acknowledgement with nack + requeue up to MaxRetries.
/// </summary>
public sealed class RabbitMqNotificationConsumer : INotificationQueueConsumer, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqNotificationConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqNotificationConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqNotificationConsumer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartConsumingAsync(
        Func<NotificationMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.Username,
            Password = _options.Password
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Prefetch one message at a time per consumer
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            NotificationMessage? message = null;
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                message = JsonSerializer.Deserialize<NotificationMessage>(json);

                if (message is null)
                {
                    _logger.LogWarning("Received null or undeserializable message. Nack without requeue.");
                    await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                    return;
                }

                message.AttemptCount++;
                await handler(message, cancellationToken);
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception ex)
            {
                var attempt = message?.AttemptCount ?? 1;
                var requeue = attempt < _options.MaxRetries;

                _logger.LogError(ex,
                    "Error processing message {MessageId} (attempt {Attempt}/{Max}). Requeue: {Requeue}",
                    message?.MessageId, attempt, _options.MaxRetries, requeue);

                if (requeue && _options.RetryDelaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds * attempt), cancellationToken);

                await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: requeue, cancellationToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Consumer started on queue '{Queue}'", _options.QueueName);

        // Hold until cancellation
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null) await _channel.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }
}
