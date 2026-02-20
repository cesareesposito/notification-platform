namespace Notification.Providers.Email.SendGrid;

public class SendGridOptions
{
    public const string SectionName = "Providers:SendGrid";

    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Default sender email used when TenantConfig.EmailFrom is not set.</summary>
    public string DefaultFromEmail { get; init; } = string.Empty;
    public string DefaultFromName { get; init; } = string.Empty;

    /// <summary>Optional SendGrid sandbox mode for testing.</summary>
    public bool SandboxMode { get; init; } = false;
}
