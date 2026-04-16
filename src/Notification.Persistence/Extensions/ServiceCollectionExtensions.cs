using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Notification.Domain.Abstractions;
using Notification.Persistence.Repositories;
using Notification.Persistence.Seeding;

namespace Notification.Persistence.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the NotificationDbContext, repository implementations, memory cache,
    /// and the DatabaseSeeder.
    /// </summary>
    public static IServiceCollection AddNotificationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:NotificationDb is required. " +
                "Add it to appsettings.json or via environment variable " +
                "ConnectionStrings__NotificationDb.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);

        // DbContext factory (thread-safe, works with Singleton and Scoped services)
        services.AddDbContextFactory<NotificationDbContext>(opts =>
            opts.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 5);
                npgsql.CommandTimeout(30);
            }));

        // In-memory cache (shared for tenant + template lookups)
        services.AddMemoryCache();

        // Tenant config — replaces StaticTenantConfigProvider
        services.AddSingleton<PostgresTenantConfigProvider>();
        services.AddSingleton<ITenantConfigProvider>(sp =>
            sp.GetRequiredService<PostgresTenantConfigProvider>());

        // Template repository — replaces FileSystemTemplateRepository
        services.AddSingleton<PostgresTemplateRepository>();
        services.AddSingleton<ITemplateRepository>(sp =>
            sp.GetRequiredService<PostgresTemplateRepository>());

        // Seeder (used directly by Program.cs at startup)
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
