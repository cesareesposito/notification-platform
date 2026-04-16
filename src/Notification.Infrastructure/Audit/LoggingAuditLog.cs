using Microsoft.Extensions.Logging;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;

namespace Notification.Infrastructure.Audit;

/// <summary>
/// Structured-logging audit sink.
/// Replace or extend with a database / OpenTelemetry sink for production.
/// </summary>
public class LoggingAuditLog : INotificationAuditLog
{
    private readonly ILogger<LoggingAuditLog> _logger;

    public LoggingAuditLog(ILogger<LoggingAuditLog> logger) => _logger = logger;

    public Task RecordAsync(
        NotificationMessage message,
        NotificationResult result,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Notification audit | MessageId={MessageId} Client={ClientId} Channel={Channel} " +
            "Recipient={Recipient} Template={Template} Status={Status} Attempt={Attempt} " +
            "ProviderMsgId={ProviderMsgId} Error={Error}",
            message.MessageId,
            message.Request.ClientId,
            message.Request.Channel,
            message.Request.Recipient,
            message.Request.TemplateName,
            result.Status,
            result.AttemptCount,
            result.ProviderMessageId ?? "-",
            result.ErrorMessage ?? "-");

        return Task.CompletedTask;
    }
}
