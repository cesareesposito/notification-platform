using Notification.Infrastructure.Extensions;
using Notification.Persistence.Extensions;
using Notification.Persistence.Seeding;
using Notification.Providers.Email.SendGrid.Extensions;
using Notification.Providers.Push.Firebase.Extensions;
using Notification.Templates.Extensions;
using Notification.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure: RabbitMQ queue/consumer, audit log
builder.Services.AddNotificationInfrastructure(builder.Configuration);

// Persistence: PostgreSQL, tenant config provider, template repository
builder.Services.AddNotificationPersistence(builder.Configuration);

// Template renderer (Scriban)
builder.Services.AddNotificationTemplates();

// Providers
builder.Services.AddSendGridEmailProvider(builder.Configuration);
builder.Services.AddFirebasePushProvider(builder.Configuration);

// Core dispatcher + hosted service
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddHostedService<NotificationWorker>();

var host = builder.Build();

// ── Migrate DB + seed on startup ─────────────────────────────────────────────
await using (var scope = host.Services.CreateAsyncScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync(
        templatesBasePath: builder.Configuration["Templates:FileSystem:BasePath"] ?? "templates");
}

host.Run();
