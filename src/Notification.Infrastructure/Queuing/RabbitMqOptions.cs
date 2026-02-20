namespace Notification.Infrastructure.Queuing;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "/";
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string QueueName { get; init; } = "notifications";
    public string ExchangeName { get; init; } = "notification.exchange";
    public string RoutingKey { get; init; } = "notification.send";

    /// <summary>Max retries before a message is dead-lettered.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Delay between retries in seconds (linear backoff).</summary>
    public int RetryDelaySeconds { get; init; } = 10;
}
