using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Notification.Persistence.Extensions;

namespace Notification.Persistence;

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__NotificationDb")
            ?? configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:NotificationDb is required for EF Core design-time operations.");

        var dataSource = ServiceCollectionExtensions.CreateDataSource(connectionString);
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        ServiceCollectionExtensions.ConfigureDbContext(optionsBuilder, dataSource);

        return new NotificationDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration()
    {
        var apiProjectPath = ResolveApiProjectPath();
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiProjectPath, "appsettings.json"), optional: false, reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            builder.AddJsonFile(
                Path.Combine(apiProjectPath, $"appsettings.{environmentName}.json"),
                optional: true,
                reloadOnChange: false);
        }

        return builder.Build();
    }

    private static string ResolveApiProjectPath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Notification.Api", "appsettings.json");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)
                    ?? throw new InvalidOperationException("Unable to determine Notification.Api directory.");
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "Unable to locate Notification.Api/appsettings.json from the current working directory.");
    }
}