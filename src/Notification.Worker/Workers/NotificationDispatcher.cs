using Notification.Domain.Abstractions;
using Notification.Domain.Models;

namespace Notification.Worker.Workers;

/// <summary>
/// Core dispatch logic: given a queued <see cref="NotificationMessage"/>:
/// 1. Resolve tenant config
/// 2. Load and render the template
/// 3. Route to the correct provider
/// 4. Record the outcome in the audit log
/// </summary>
public class NotificationDispatcher
{
    private readonly ITenantConfigProvider _tenantConfigProvider;
    private readonly ITemplateRepository _templateRepository;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly INotificationAuditLog _auditLog;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        ITenantConfigProvider tenantConfigProvider,
        ITemplateRepository templateRepository,
        ITemplateRenderer templateRenderer,
        IEnumerable<INotificationProvider> providers,
        INotificationAuditLog auditLog,
        ILogger<NotificationDispatcher> logger)
    {
        _tenantConfigProvider = tenantConfigProvider;
        _templateRepository = templateRepository;
        _templateRenderer = templateRenderer;
        _providers = providers;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task DispatchAsync(NotificationMessage message, CancellationToken ct)
    {
        var req = message.Request;

        // 1. Tenant config
        var tenantConfig = await _tenantConfigProvider.GetConfigAsync(req.TenantId, ct);
        if (tenantConfig is null)
        {
            _logger.LogError(
                "Tenant '{TenantId}' not found for message {MessageId}. Dropping.",
                req.TenantId, message.MessageId);

            await _auditLog.RecordAsync(message,
                NotificationResult.Failure(message.MessageId, $"Tenant '{req.TenantId}' not found.", message.AttemptCount), ct);
            return;
        }

        // 2. Load template
        var templateKey = new NotificationTemplateKey(req.TenantId, req.TemplateName, req.Channel, req.Language);
        var rawTemplate = await _templateRepository.GetTemplateAsync(templateKey, ct);

        if (rawTemplate is null)
        {
            _logger.LogError(
                "Template '{Name}' not found for tenant '{TenantId}', channel {Channel}. Dropping.",
                req.TemplateName, req.TenantId, req.Channel);

            await _auditLog.RecordAsync(message,
                NotificationResult.Failure(message.MessageId, $"Template '{req.TemplateName}' not found.", message.AttemptCount), ct);
            return;
        }

        // 3. Render template
        var rendered = await _templateRenderer.RenderAsync(rawTemplate, req.Data, ct);

        // 4. Pick provider
        var providerName = req.Channel switch
        {
            NotificationChannel.Email => tenantConfig.EmailProvider,
            NotificationChannel.Push or NotificationChannel.WebPush => tenantConfig.PushProvider,
            _ => throw new InvalidOperationException($"Unsupported channel: {req.Channel}")
        };

        var provider = _providers.FirstOrDefault(p =>
            p.Channel == req.Channel &&
            p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            var error = $"No provider registered for channel {req.Channel} / name '{providerName}'.";
            _logger.LogError("{Error} MessageId={MessageId}", error, message.MessageId);
            await _auditLog.RecordAsync(message,
                NotificationResult.Failure(message.MessageId, error, message.AttemptCount), ct);
            return;
        }

        // 5. Send
        var providerMessageId = await provider.SendAsync(req, rendered, tenantConfig, ct);

        // 6. Audit
        var result = NotificationResult.Success(message.MessageId, providerMessageId, message.AttemptCount);
        await _auditLog.RecordAsync(message, result, ct);

        _logger.LogInformation(
            "Dispatched {Channel} notification {MessageId} via {Provider} for tenant {TenantId}",
            req.Channel, message.MessageId, providerName, req.TenantId);
    }
}
