using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Domain.Abstractions;

namespace Notification.Providers.Email.Smtp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpEmailProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<INotificationProvider, SmtpEmailProvider>();
        return services;
    }
}
