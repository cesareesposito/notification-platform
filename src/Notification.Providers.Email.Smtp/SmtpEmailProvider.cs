using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;

namespace Notification.Providers.Email.Smtp;

public class SmtpEmailProvider : INotificationProvider
{
    public const string Name = "Smtp";
    public string ProviderName => Name;
    public NotificationChannel Channel => NotificationChannel.Email;

    // Purpose string must match the one used in AdminController when encrypting.
    private const string DataProtectionPurpose = "SmtpPassword";

    private readonly SmtpOptions _defaultOptions;
    private readonly IDataProtector _protector;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(
        IOptions<SmtpOptions> options,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SmtpEmailProvider> logger)
    {
        _defaultOptions = options.Value;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _logger = logger;
    }

    public async Task<string?> SendAsync(
        NotificationRequest request,
        RenderedTemplate template,
        TenantConfig tenantConfig,
        CancellationToken cancellationToken = default)
    {
        var settings = tenantConfig.ProviderSettings;

        var host = settings.TryGetValue("Smtp:Host", out var h) ? h : _defaultOptions.Host;
        var port = settings.TryGetValue("Smtp:Port", out var p) && int.TryParse(p, out var portNum)
            ? portNum : _defaultOptions.Port;
        var username = settings.TryGetValue("Smtp:Username", out var u) ? u : _defaultOptions.Username;
        var useSsl = settings.TryGetValue("Smtp:UseSsl", out var ssl)
            ? bool.TryParse(ssl, out var sslVal) && sslVal
            : _defaultOptions.UseSsl;
        var skipCert = settings.TryGetValue("Smtp:SkipCertificateValidation", out var sc)
            ? bool.TryParse(sc, out var scVal) && scVal
            : _defaultOptions.SkipCertificateValidation;

        // Decrypt per-tenant password stored encrypted in DB; fall back to global config.
        string? password = null;
        if (settings.TryGetValue("Smtp:Password", out var encryptedPwd) && !string.IsNullOrEmpty(encryptedPwd))
        {
            try
            {
                password = _protector.Unprotect(encryptedPwd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt SMTP password for client {ClientId}", tenantConfig.ClientId);
                throw new InvalidOperationException(
                    $"Cannot decrypt SMTP password for client '{tenantConfig.ClientId}'.", ex);
            }
        }
        else
        {
            password = _defaultOptions.Password;
        }

        var fromEmail = tenantConfig.EmailFrom ?? _defaultOptions.DefaultFromEmail;
        var fromName = tenantConfig.EmailFromName ?? _defaultOptions.DefaultFromName;

        using var message = new MailMessage();
        message.From = new MailAddress(fromEmail, fromName);
        message.To.Add(new MailAddress(request.Recipient, request.RecipientName));
        message.Subject = template.Subject ?? string.Empty;
        message.IsBodyHtml = !string.IsNullOrWhiteSpace(template.HtmlBody);
        message.Body = message.IsBodyHtml
            ? template.HtmlBody ?? string.Empty
            : template.TextBody ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(template.HtmlBody) && !string.IsNullOrWhiteSpace(template.TextBody))
        {
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(template.TextBody, null, "text/plain"));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(template.HtmlBody, null, "text/html"));
        }

        // System.Net.Mail: EnableSsl=true means STARTTLS on port 587 or implicit SSL on port 465.
        // Derive from the well-known port rather than the stored UseSsl flag, which had different
        // semantics under MailKit (where UseSsl=false on 587 meant STARTTLS, not "no TLS").
        bool enableSsl = port switch
        {
            587 => true,   // STARTTLS required
            465 => true,   // Implicit SSL
            _ => useSsl    // Fall back to explicit config for non-standard ports
        };

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(username) && password is not null)
            client.Credentials = new NetworkCredential(username, password);

        if (skipCert)
            _logger.LogWarning(
                "Smtp:SkipCertificateValidation is enabled for client {ClientId}, but System.Net.Mail does not support per-client certificate validation bypass. The setting will be ignored.",
                tenantConfig.ClientId);

        _logger.LogInformation(
            "SMTP send starting for client {ClientId} to {Recipient} via {Host}:{Port} using System.Net.Mail (EnableSsl={EnableSsl}, AuthUser={Username}, SkipCert={SkipCert})",
            tenantConfig.ClientId,
            request.Recipient,
            host,
            port,
            enableSsl,
            string.IsNullOrWhiteSpace(username) ? "<none>" : username,
            skipCert);

        try
        {
            await client.SendMailAsync(message).WaitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SMTP send failed for client {ClientId} to {Recipient} via {Host}:{Port} using System.Net.Mail (EnableSsl={EnableSsl})",
                tenantConfig.ClientId,
                request.Recipient,
                host,
                port,
                enableSsl);
            throw;
        }

        _logger.LogDebug(
            "Email sent via SMTP to {Recipient}, server={Host}:{Port}",
            request.Recipient, host, port);

        return $"smtp-{Guid.NewGuid():N}";
    }
}
