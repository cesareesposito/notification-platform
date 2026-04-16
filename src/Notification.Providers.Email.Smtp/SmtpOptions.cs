namespace Notification.Providers.Email.Smtp;

/// <summary>
/// Global SMTP fallback settings (section "Providers:Smtp" in appsettings).
/// Per-tenant overrides are stored encrypted in TenantConfig.ProviderSettings.
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Providers:Smtp";

    /// <summary>SMTP server hostname or IP.</summary>
    public string Host { get; init; } = "smtp.gmail.com";

    /// <summary>SMTP port. Common values: 587 (STARTTLS), 465 (SSL), 25 (plain).</summary>
    public int Port { get; init; } = 587;

    /// <summary>SMTP authentication username. Null = no auth.</summary>
    public string? Username { get; init; }

    /// <summary>
    /// SMTP authentication password (global fallback, loaded from config/env vars in plaintext).
    /// Per-tenant passwords in the DB are stored encrypted.
    /// </summary>
    public string? Password { get; init; } = "jept ukow kmie yvcn";

    /// <summary>Use SSL/TLS on connect (port 465). False = STARTTLS when available.</summary>
    public bool UseSsl { get; init; } = false;

    /// <summary>Skip TLS certificate validation. Use only for self-signed certs in dev/test.</summary>
    public bool SkipCertificateValidation { get; init; } = false;

    public string DefaultFromEmail { get; init; } = "teamcareassistant@gmail.com";
    public string DefaultFromName { get; init; } = "Notification Platform";
}
