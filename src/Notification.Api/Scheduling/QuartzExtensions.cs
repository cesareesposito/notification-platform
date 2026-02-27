using Quartz;

namespace Notification.Api.Scheduling;

public static class QuartzExtensions
{
    public static IServiceCollection AddNotificationScheduling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException("ConnectionStrings:NotificationDb is required for Quartz job store.");

        services.AddQuartz(q =>
        {
            q.SchedulerId = "AUTO";
            q.SchedulerName = "NotificationScheduler";

            q.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.UsePostgres(pg =>
                {
                    pg.ConnectionString = connectionString;
                    pg.TablePrefix = "QRTZ_";
                });
                store.UseNewtonsoftJsonSerializer();
                store.UseClustering();
            });
        });

        services.AddQuartzHostedService(opts =>
        {
            opts.WaitForJobsToComplete = true;
            opts.AwaitApplicationStarted = true;
        });

        return services;
    }
}
