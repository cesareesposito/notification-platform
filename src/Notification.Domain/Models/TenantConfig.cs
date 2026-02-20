namespace Notification.Domain.Models;

/// <summary>
/// Per-tenant provider and routing configuration.
/// </summary>
public class TenantConfig
{
    public string TenantId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    // Email
    public string EmailProvider { get; init; } = "SendGrid"; // SendGrid | SES | Mailgun
    public string? EmailFrom { get; init; }
    public string? EmailFromName { get; init; }

    // Push
    public string PushProvider { get; init; } = "Firebase"; // Firebase | APNs

    // Template root path for this tenant (e.g. /templates/tenant-a/)
    public string TemplateBasePath { get; init; } = string.Empty;

    // Provider-specific settings (API keys, credentials, etc.)
    public Dictionary<string, string> ProviderSettings { get; init; } = new();

    // Rate limiting: max notifications per minute
    public int RateLimitPerMinute { get; init; } = 100;
}
