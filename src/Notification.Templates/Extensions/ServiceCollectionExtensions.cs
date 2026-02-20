using Microsoft.Extensions.DependencyInjection;
using Notification.Domain.Abstractions;

namespace Notification.Templates.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Scriban template renderer.
    /// Note: ITemplateRepository is now registered by AddNotificationPersistence().
    /// </summary>
    public static IServiceCollection AddNotificationTemplates(
        this IServiceCollection services)
    {
        services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();
        return services;
    }
}
