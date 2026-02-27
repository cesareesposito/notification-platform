using Microsoft.AspNetCore.DataProtection;
using Notification.Api.Middleware;
using Notification.Api.Scheduling;
using Notification.Infrastructure.Extensions;
using Notification.Persistence.Extensions;
using Notification.Persistence.Seeding;
using Notification.Templates.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Data Protection: shared key ring with Notification.Worker for SMTP password encryption.
// Both services must mount the same keys directory (e.g. /keys volume in Docker).
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "/keys"))
    .SetApplicationName("notification-platform");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Notification Platform API", Version = "v1" });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("NotificationDb")!,
        name: "postgres",
        tags: ["db", "ready"]);

// Infrastructure: RabbitMQ queue, audit log
builder.Services.AddNotificationInfrastructure(builder.Configuration);

// Persistence: PostgreSQL DbContext, tenant config provider, template repository
builder.Services.AddNotificationPersistence(builder.Configuration);

// Template renderer (Scriban) — repository is provided by Persistence
builder.Services.AddNotificationTemplates();

// Quartz.NET scheduling (PostgreSQL job store)
builder.Services.AddNotificationScheduling(builder.Configuration);

var app = builder.Build();

// ── Migrate DB + seed initial data on startup ─────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync(
        templatesBasePath: builder.Configuration["Templates:FileSystem:BasePath"] ?? "templates");
}

// ── Initialize Quartz schema (idempotent) ─────────────────────────────────────
await QuartzSchemaInitializer.InitializeAsync(
    connectionString: builder.Configuration.GetConnectionString("NotificationDb")!,
    logger: app.Logger);

app.UseMiddleware<ApiKeyAuthMiddleware>();

// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

app.UseSwagger();
app.UseSwaggerUI();


app.MapHealthChecks("/health");
app.MapControllers();

app.MapGet("/", context =>
    {
        context.Response.Redirect("/api/swagger/index.html");
        return Task.CompletedTask;
    });

app.Run();
