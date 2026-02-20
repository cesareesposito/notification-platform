using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Domain.Abstractions;

namespace Notification.Providers.Email.SendGrid.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSendGridEmailProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SendGridOptions>(
            configuration.GetSection(SendGridOptions.SectionName));

        services.AddSingleton<INotificationProvider, SendGridEmailProvider>();

        return services;
    }
}
