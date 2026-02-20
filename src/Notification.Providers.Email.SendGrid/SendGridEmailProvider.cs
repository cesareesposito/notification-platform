using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Notification.Providers.Email.SendGrid;

public class SendGridEmailProvider : INotificationProvider
{
    public string ProviderName => "SendGrid";
    public NotificationChannel Channel => NotificationChannel.Email;

    private readonly SendGridOptions _defaultOptions;
    private readonly ILogger<SendGridEmailProvider> _logger;

    public SendGridEmailProvider(
        IOptions<SendGridOptions> options,
        ILogger<SendGridEmailProvider> logger)
    {
        _defaultOptions = options.Value;
        _logger = logger;
    }

    public async Task<string?> SendAsync(
        NotificationRequest request,
        RenderedTemplate template,
        TenantConfig tenantConfig,
        CancellationToken cancellationToken = default)
    {
        // Tenant can override the API key stored in ProviderSettings
        var apiKey = tenantConfig.ProviderSettings.TryGetValue("SendGrid:ApiKey", out var key)
            ? key
            : _defaultOptions.ApiKey;

        var client = new SendGridClient(apiKey);

        var fromEmail = tenantConfig.EmailFrom ?? _defaultOptions.DefaultFromEmail;
        var fromName = tenantConfig.EmailFromName ?? _defaultOptions.DefaultFromName;

        var msg = new SendGridMessage
        {
            From = new EmailAddress(fromEmail, fromName),
            Subject = template.Subject
        };

        msg.AddTo(new EmailAddress(request.Recipient, request.RecipientName));

        if (!string.IsNullOrWhiteSpace(template.HtmlBody))
            msg.HtmlContent = template.HtmlBody;

        if (!string.IsNullOrWhiteSpace(template.TextBody))
            msg.PlainTextContent = template.TextBody;

        // Sandbox mode for testing
        if (_defaultOptions.SandboxMode)
            msg.SetSandBoxMode(true);

        var response = await client.SendEmailAsync(msg, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "SendGrid returned {StatusCode} for {Recipient}: {Body}",
                response.StatusCode, request.Recipient, body);
            throw new InvalidOperationException($"SendGrid error {response.StatusCode}: {body}");
        }

        // SendGrid message ID is in the X-Message-Id header
        response.Headers.TryGetValues("X-Message-Id", out var ids);
        var messageId = ids?.FirstOrDefault();

        _logger.LogDebug(
            "Email sent via SendGrid to {Recipient}, msgId={MessageId}",
            request.Recipient, messageId);

        return messageId;
    }
}
