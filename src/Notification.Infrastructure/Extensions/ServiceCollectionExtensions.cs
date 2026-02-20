using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Domain.Abstractions;
using Notification.Infrastructure.Audit;
using Notification.Infrastructure.Queuing;

namespace Notification.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers infrastructure services: RabbitMQ queue/consumer, audit log.
    /// Note: ITenantConfigProvider is now registered by AddNotificationPersistence().
    /// </summary>
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(
            configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddSingleton<INotificationQueue, RabbitMqNotificationQueue>();
        services.AddSingleton<INotificationQueueConsumer, RabbitMqNotificationConsumer>();
        services.AddSingleton<INotificationAuditLog, LoggingAuditLog>();

        return services;
    }
}
