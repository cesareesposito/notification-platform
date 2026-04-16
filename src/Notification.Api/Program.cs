using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
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

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JwtAuth:Secret"]
    ?? throw new InvalidOperationException("JwtAuth:Secret must be configured.");
var jwtIssuer = builder.Configuration["JwtAuth:Issuer"] ?? "notification-platform";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "admin"));

    options.AddPolicy("AnyAuth", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  ctx.User.HasClaim("scope", "admin") ||
                  ctx.User.HasClaim("scope", "client")));
});

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

app.UseAuthentication();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseAuthorization();

// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

app.UseSwagger();
app.UseSwaggerUI();

// ── Serve Admin SPA from wwwroot/admin/browser ────────────────────────────────
var adminBrowserPath = Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "admin", "browser");

// Serve static assets (JS/CSS/etc.) under /admin/** with extension
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/admin",
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(adminBrowserPath)
});

// Default static files for everything else under wwwroot root
app.UseStaticFiles();

// SPA fallback: any /admin/** path that isn't a file → serve index.html
app.MapWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/admin") &&
           !ctx.Request.Path.Value!.Contains('.'),
    adminApp =>
    {
        adminApp.Run(async ctx =>
        {
            var indexPath = Path.Combine(adminBrowserPath, "index.html");
            ctx.Response.ContentType = "text/html";
            await ctx.Response.SendFileAsync(indexPath);
        });
    });


app.MapHealthChecks("/health");
app.MapControllers();

app.MapGet("/", context =>
    {
        context.Response.Redirect("/api/swagger/index.html");
        return Task.CompletedTask;
    });

app.Run();
