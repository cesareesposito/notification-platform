using System.Text.Json;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;
using Quartz;

namespace Notification.Api.Scheduling.Jobs;

/// <summary>
/// Quartz job that publishes a scheduled notification to the RabbitMQ queue.
/// The NotificationRequest is stored as JSON in the JobDataMap under the key "request".
/// </summary>
[DisallowConcurrentExecution]
public class PublishNotificationJob : IJob
{
    public const string RequestDataKey = "request";

    private readonly INotificationQueue _queue;
    private readonly ILogger<PublishNotificationJob> _logger;

    public PublishNotificationJob(INotificationQueue queue, ILogger<PublishNotificationJob> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var json = context.JobDetail.JobDataMap.GetString(RequestDataKey);
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogError("Job {JobKey} has no request data in JobDataMap — skipping", context.JobDetail.Key);
            return;
        }

        NotificationRequest request;
        try
        {
            request = JsonSerializer.Deserialize<NotificationRequest>(json)
                ?? throw new InvalidOperationException("Deserialized request is null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobKey} failed to deserialize NotificationRequest — skipping", context.JobDetail.Key);
            return;
        }

        var message = new NotificationMessage { Request = request };

        try
        {
            await _queue.PublishAsync(message, context.CancellationToken);
            _logger.LogInformation(
                "Job {JobKey} published notification {MessageId} for client {ClientId} ({Channel})",
                context.JobDetail.Key, message.MessageId, request.ClientId, request.Channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobKey} failed to publish notification for client {ClientId}",
                context.JobDetail.Key, request.ClientId);
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
