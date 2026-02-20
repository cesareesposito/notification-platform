namespace Notification.Api.Middleware;

/// <summary>
/// Simple API-key authentication middleware.
/// Expects the key in the X-Api-Key header.
/// Set ApiAuth:Keys in configuration to a list of valid keys.
/// Disable or replace with OAuth/JWT in production as needed.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var validKeys = _configuration.GetSection("ApiAuth:Keys").Get<string[]>() ?? [];

        if (validKeys.Length == 0)
        {
            // No keys configured → auth disabled (dev mode)
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
            || !validKeys.Contains(providedKey.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key." });
            return;
        }

        await _next(context);
    }
}
