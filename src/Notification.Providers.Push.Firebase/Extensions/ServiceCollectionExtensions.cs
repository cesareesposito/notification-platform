using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Domain.Abstractions;

namespace Notification.Providers.Push.Firebase.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFirebasePushProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FirebaseOptions>(
            configuration.GetSection(FirebaseOptions.SectionName));

        services.AddSingleton<INotificationProvider, FirebasePushProvider>();

        return services;
    }
}
