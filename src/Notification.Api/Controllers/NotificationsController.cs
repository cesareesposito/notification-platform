using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.Api.Requests;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;

namespace Notification.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
[Authorize(Policy = "AnyAuth")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationQueue _queue;
    private readonly ITenantConfigProvider _tenantConfigProvider;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationQueue queue,
        ITenantConfigProvider tenantConfigProvider,
        ILogger<NotificationsController> logger)
    {
        _queue = queue;
        _tenantConfigProvider = tenantConfigProvider;
        _logger = logger;
    }

    /// <summary>Enqueue a single email notification.</summary>
    [HttpPost("email")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendEmail(
        [FromBody] SendEmailRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        var clientId = ResolveClientId(req.ClientId);
        if (clientId is null)
            return Forbid();

        var tenantConfig = await _tenantConfigProvider.GetConfigAsync(clientId, ct);
        if (tenantConfig is null)
            return NotFound(new { error = $"Client '{clientId}' not found." });

        var message = BuildMessage(clientId, NotificationChannel.Email, req.Recipient,
            req.RecipientName, req.TemplateName, req.Language, req.Data,
            req.IdempotencyKey, req.ScheduledAt);

        await _queue.PublishAsync(message, ct);

        _logger.LogInformation(
            "Enqueued email notification {MessageId} for client {ClientId}",
            message.MessageId, clientId);

        return Accepted(new { messageId = message.MessageId });
    }

    /// <summary>Enqueue a single push notification.</summary>
    [HttpPost("push")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendPush(
        [FromBody] SendPushRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        var clientId = ResolveClientId(req.ClientId);
        if (clientId is null)
            return Forbid();

        var tenantConfig = await _tenantConfigProvider.GetConfigAsync(clientId, ct);
        if (tenantConfig is null)
            return NotFound(new { error = $"Client '{clientId}' not found." });

        var message = BuildMessage(clientId, NotificationChannel.Push, req.DeviceToken,
            req.RecipientName, req.TemplateName, req.Language, req.Data,
            req.IdempotencyKey, req.ScheduledAt);

        await _queue.PublishAsync(message, ct);

        _logger.LogInformation(
            "Enqueued push notification {MessageId} for client {ClientId}",
            message.MessageId, clientId);

        return Accepted(new { messageId = message.MessageId });
    }

    /// <summary>Enqueue bulk notifications (up to 1 000 recipients per call).</summary>
    [HttpPost("bulk")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendBulk(
        [FromBody] SendBulkRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        var clientId = ResolveClientId(req.ClientId);
        if (clientId is null)
            return Forbid();

        var tenantConfig = await _tenantConfigProvider.GetConfigAsync(clientId, ct);
        if (tenantConfig is null)
            return NotFound(new { error = $"Client '{clientId}' not found." });

        var messageIds = new List<Guid>(req.Recipients.Count);

        foreach (var recipient in req.Recipients)
        {
            // Merge shared data with per-recipient overrides
            var data = new Dictionary<string, object?>(req.SharedData);
            foreach (var (k, v) in recipient.Data) data[k] = v;

            var message = BuildMessage(clientId, req.Channel, recipient.Recipient,
                recipient.RecipientName, req.TemplateName, req.Language, data,
                idempotencyKey: null, scheduledAt: null);

            await _queue.PublishAsync(message, ct);
            messageIds.Add(message.MessageId);
        }

        _logger.LogInformation(
            "Enqueued {Count} bulk notifications for client {ClientId}",
            messageIds.Count, clientId);

        return Accepted(new { count = messageIds.Count, messageIds });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static NotificationMessage BuildMessage(
        string clientId,
        NotificationChannel channel,
        string recipient,
        string? recipientName,
        string templateName,
        string language,
        Dictionary<string, object?> data,
        string? idempotencyKey,
        DateTimeOffset? scheduledAt)
    {
        return new NotificationMessage
        {
            Request = new NotificationRequest
            {
                ClientId = clientId,
                Channel = channel,
                Recipient = recipient,
                RecipientName = recipientName,
                TemplateName = templateName,
                Language = language,
                Data = data,
                IdempotencyKey = idempotencyKey,
                ScheduledAt = scheduledAt
            }
        };
    }

    private bool IsAdminScope() =>
        User.HasClaim("scope", "admin");

    private string? GetClientId() =>
        User.FindFirst("notificationClientId")?.Value;

    private string? ResolveClientId(string? requestedClientId)
    {
        if (IsAdminScope())
            return requestedClientId;

        return GetClientId();
    }
}
