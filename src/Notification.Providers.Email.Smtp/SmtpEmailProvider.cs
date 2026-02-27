using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
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
                _logger.LogError(ex, "Failed to decrypt SMTP password for tenant {TenantId}", tenantConfig.TenantId);
                throw new InvalidOperationException(
                    $"Cannot decrypt SMTP password for tenant '{tenantConfig.TenantId}'.", ex);
            }
        }
        else
        {
            password = _defaultOptions.Password;
        }

        var fromEmail = tenantConfig.EmailFrom ?? _defaultOptions.DefaultFromEmail;
        var fromName = tenantConfig.EmailFromName ?? _defaultOptions.DefaultFromName;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress(request.RecipientName, request.Recipient));
        message.Subject = template.Subject ?? string.Empty;

        var bodyBuilder = new BodyBuilder();
        if (!string.IsNullOrWhiteSpace(template.HtmlBody))
            bodyBuilder.HtmlBody = template.HtmlBody;
        if (!string.IsNullOrWhiteSpace(template.TextBody))
            bodyBuilder.TextBody = template.TextBody;
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        if (skipCert)
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;

        var socketOption = useSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(host, port, socketOption, cancellationToken);

        if (!string.IsNullOrWhiteSpace(username) && password is not null)
            await client.AuthenticateAsync(username, password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogDebug(
            "Email sent via SMTP to {Recipient}, server={Host}:{Port}",
            request.Recipient, host, port);

        return message.MessageId;
    }
}
