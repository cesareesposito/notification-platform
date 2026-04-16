using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;
using RabbitMQ.Client;

namespace Notification.Infrastructure.Queuing;

/// <summary>
/// Publishes <see cref="NotificationMessage"/> to a RabbitMQ exchange.
/// Used by the API to enqueue outbound notifications.
/// </summary>
public sealed class RabbitMqNotificationQueue : INotificationQueue, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqNotificationQueue> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public RabbitMqNotificationQueue(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqNotificationQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.MessageId.ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _channel!.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogDebug(
            "Published message {MessageId} for client {ClientId} via channel {Channel}",
            message.MessageId, message.Request.ClientId, message.Request.Channel);
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_channel is { IsOpen: true }) return;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.Username,
                Password = _options.Password
            };

            _connection = await factory.CreateConnectionAsync(ct);
            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            // Declare dead-letter exchange + queue
            await _channel.ExchangeDeclareAsync(
                exchange: $"{_options.ExchangeName}.dlx",
                type: ExchangeType.Direct,
                durable: true,
                cancellationToken: ct);

            await _channel.QueueDeclareAsync(
                queue: $"{_options.QueueName}.dead-letter",
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct);

            await _channel.QueueBindAsync(
                queue: $"{_options.QueueName}.dead-letter",
                exchange: $"{_options.ExchangeName}.dlx",
                routingKey: _options.RoutingKey,
                cancellationToken: ct);

            // Main exchange + queue with dead-letter config
            await _channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                cancellationToken: ct);

            var args = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = $"{_options.ExchangeName}.dlx",
                ["x-dead-letter-routing-key"] = _options.RoutingKey
            };

            await _channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: args,
                cancellationToken: ct);

            await _channel.QueueBindAsync(
                queue: _options.QueueName,
                exchange: _options.ExchangeName,
                routingKey: _options.RoutingKey,
                cancellationToken: ct);

            _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", _options.Host, _options.Port);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null) await _channel.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }
}
