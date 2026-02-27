using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Notification.Api.Requests;
using Notification.Api.Scheduling.Jobs;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;
using Quartz;

namespace Notification.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/scheduled")]
public class ScheduledNotificationsController : ControllerBase
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ITenantConfigProvider _tenantConfigProvider;
    private readonly ILogger<ScheduledNotificationsController> _logger;

    private const string OnceGroup    = "once";
    private const string PeriodicGroup = "periodic";

    public ScheduledNotificationsController(
        ISchedulerFactory schedulerFactory,
        ITenantConfigProvider tenantConfigProvider,
        ILogger<ScheduledNotificationsController> logger)
    {
        _schedulerFactory = schedulerFactory;
        _tenantConfigProvider = tenantConfigProvider;
        _logger = logger;
    }

    // ── One-shot ─────────────────────────────────────────────────────────────

    /// <summary>Schedule a single email notification at a specific date/time.</summary>
    [HttpPost("email/once")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScheduleEmailOnce(
        [FromBody] ScheduleEmailRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();
        if (req.ScheduledAt <= DateTimeOffset.UtcNow)
            return BadRequest(new { error = "ScheduledAt must be in the future." });

        var tenantConfig = await _tenantConfigProvider.GetConfigAsync(req.TenantId, ct);
        if (tenantConfig is null)
            return NotFound(new { error = $"Tenant '{req.TenantId}' not found." });

        var request = BuildRequest(req.TenantId, NotificationChannel.Email,
            req.Recipient, req.RecipientName, req.TemplateName, req.Language,
            req.Data, req.IdempotencyKey, req.ScheduledAt);

        var jobId = await ScheduleOnceAsync(request, NotificationChannel.Email, req.ScheduledAt, ct);

        _logger.LogInformation(
            "Scheduled one-time email {JobId} for tenant {TenantId} at {ScheduledAt}",
            jobId, req.TenantId, req.ScheduledAt);

        return CreatedAtAction(nameof(GetJob), new { jobId },
            new { jobId, type = "once", channel = "Email", tenantId = req.TenantId, scheduledAt = req.ScheduledAt });
    }

    /// <summary>Schedule a single push notification at a specific date/time.</summary>
    [HttpPost("push/once")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SchedulePushOnce(
        [FromBody] SchedulePushRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();
        if (req.ScheduledAt <= DateTimeOffset.UtcNow)
            return BadRequest(new { error = "ScheduledAt must be in the future." });

        var tenantConfig = await _tenantConfigProvider.GetConfigAsync(req.TenantId, ct);
        if (tenantConfig is null)
            return NotFound(new { error = $"Tenant '{req.TenantId}' not found." });

        var request = BuildRequest(req.TenantId, NotificationChannel.Push,
            req.DeviceToken, req.RecipientName, req.TemplateName, req.Language,
            req.Data, req.IdempotencyKey, req.ScheduledAt);

        var jobId = await ScheduleOnceAsync(request, NotificationChannel.Push, req.ScheduledAt, ct);

        _logger.LogInformation(
            "Scheduled one-time push {JobId} for tenant {TenantId} at {ScheduledAt}",
            jobId, req.TenantId, req.ScheduledAt);

        return CreatedAtAction(nameof(GetJob), new { jobId },
            new { jobId, type = "once", channel = "Push", tenantId = req.TenantId, scheduledAt = req.ScheduledAt });
    }

    // ── Periodic (cron) ──────────────────────────────────────────────────────

    /// <summary>Schedule a recurring email notification using a cron expression.</summary>
    [HttpPost("email/periodic")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScheduleEmailPeriodic(
        [FromBody] PeriodicEmailRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();
        if (!CronExpression.IsValidExpression(req.CronExpression))
            return BadRequest(new { error = $"Invalid cron expression: '{req.CronExpression}'." });

        var tenantConfig = await _tenantConfigProvider.GetConfigAsync(req.TenantId, ct);
        if (tenantConfig is null)
            return NotFound(new { error = $"Tenant '{req.TenantId}' not found." });

        var request = BuildRequest(req.TenantId, NotificationChannel.Email,
            req.Recipient, req.RecipientName, req.TemplateName, req.Language,
            req.Data, req.IdempotencyKey, scheduledAt: null);

        var jobId = await SchedulePeriodicAsync(request, NotificationChannel.Email,
            req.CronExpression, req.StartAt, req.EndAt, ct);

        _logger.LogInformation(
            "Scheduled periodic email {JobId} for tenant {TenantId} with cron '{Cron}'",
            jobId, req.TenantId, req.CronExpression);

        return CreatedAtAction(nameof(GetJob), new { jobId },
            new { jobId, type = "periodic", channel = "Email", tenantId = req.TenantId,
                  cron = req.CronExpression, startAt = req.StartAt, endAt = req.EndAt });
    }

    /// <summary>Schedule a recurring push notification using a cron expression.</summary>
    [HttpPost("push/periodic")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SchedulePushPeriodic(
        [FromBody] PeriodicPushRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();
        if (!CronExpression.IsValidExpression(req.CronExpression))
            return BadRequest(new { error = $"Invalid cron expression: '{req.CronExpression}'." });

        var tenantConfig = await _tenantConfigProvider.GetConfigAsync(req.TenantId, ct);
        if (tenantConfig is null)
            return NotFound(new { error = $"Tenant '{req.TenantId}' not found." });

        var request = BuildRequest(req.TenantId, NotificationChannel.Push,
            req.DeviceToken, req.RecipientName, req.TemplateName, req.Language,
            req.Data, req.IdempotencyKey, scheduledAt: null);

        var jobId = await SchedulePeriodicAsync(request, NotificationChannel.Push,
            req.CronExpression, req.StartAt, req.EndAt, ct);

        _logger.LogInformation(
            "Scheduled periodic push {JobId} for tenant {TenantId} with cron '{Cron}'",
            jobId, req.TenantId, req.CronExpression);

        return CreatedAtAction(nameof(GetJob), new { jobId },
            new { jobId, type = "periodic", channel = "Push", tenantId = req.TenantId,
                  cron = req.CronExpression, startAt = req.StartAt, endAt = req.EndAt });
    }

    // ── Management ───────────────────────────────────────────────────────────

    /// <summary>List all scheduled jobs.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListJobs(CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        var result = new List<object>();

        foreach (var group in new[] { OnceGroup, PeriodicGroup })
        {
            var keys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(group), ct);
            foreach (var key in keys)
            {
                var dto = await BuildJobDtoAsync(scheduler, key, ct);
                if (dto is not null) result.Add(dto);
            }
        }

        return Ok(result);
    }

    /// <summary>Get details of a specific scheduled job.</summary>
    [HttpGet("{jobId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(string jobId, CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);

        var key = await FindJobKeyAsync(scheduler, jobId, ct);
        if (key is null) return NotFound(new { error = $"Job '{jobId}' not found." });

        var dto = await BuildJobDtoAsync(scheduler, key, ct);
        return dto is not null ? Ok(dto) : NotFound(new { error = $"Job '{jobId}' not found." });
    }

    /// <summary>Cancel and delete a scheduled job.</summary>
    [HttpDelete("{jobId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteJob(string jobId, CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);

        var key = await FindJobKeyAsync(scheduler, jobId, ct);
        if (key is null) return NotFound(new { error = $"Job '{jobId}' not found." });

        await scheduler.DeleteJob(key, ct);
        _logger.LogInformation("Deleted scheduled job {JobId}", jobId);
        return NoContent();
    }

    /// <summary>Pause a scheduled job (its trigger will not fire until resumed).</summary>
    [HttpPut("{jobId}/pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PauseJob(string jobId, CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);

        var key = await FindJobKeyAsync(scheduler, jobId, ct);
        if (key is null) return NotFound(new { error = $"Job '{jobId}' not found." });

        await scheduler.PauseJob(key, ct);
        _logger.LogInformation("Paused scheduled job {JobId}", jobId);
        return Ok(new { jobId, status = "paused" });
    }

    /// <summary>Resume a paused scheduled job.</summary>
    [HttpPut("{jobId}/resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeJob(string jobId, CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);

        var key = await FindJobKeyAsync(scheduler, jobId, ct);
        if (key is null) return NotFound(new { error = $"Job '{jobId}' not found." });

        await scheduler.ResumeJob(key, ct);
        _logger.LogInformation("Resumed scheduled job {JobId}", jobId);
        return Ok(new { jobId, status = "resumed" });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> ScheduleOnceAsync(
        NotificationRequest request,
        NotificationChannel channel,
        DateTimeOffset scheduledAt,
        CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        var jobId = Guid.NewGuid().ToString("N");

        var jobDetail = JobBuilder.Create<PublishNotificationJob>()
            .WithIdentity(jobId, OnceGroup)
            .UsingJobData(PublishNotificationJob.RequestDataKey, SerializeRequest(request))
            .UsingJobData("tenantId", request.TenantId)
            .UsingJobData("channel", channel.ToString())
            .UsingJobData("recipient", request.Recipient)
            .UsingJobData("templateName", request.TemplateName)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(jobId, OnceGroup)
            .StartAt(scheduledAt)
            .WithSimpleSchedule(s => s.WithRepeatCount(0))
            .Build();

        await scheduler.ScheduleJob(jobDetail, trigger, ct);
        return jobId;
    }

    private async Task<string> SchedulePeriodicAsync(
        NotificationRequest request,
        NotificationChannel channel,
        string cronExpression,
        DateTimeOffset? startAt,
        DateTimeOffset? endAt,
        CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        var jobId = Guid.NewGuid().ToString("N");

        var jobDetail = JobBuilder.Create<PublishNotificationJob>()
            .WithIdentity(jobId, PeriodicGroup)
            .UsingJobData(PublishNotificationJob.RequestDataKey, SerializeRequest(request))
            .UsingJobData("tenantId", request.TenantId)
            .UsingJobData("channel", channel.ToString())
            .UsingJobData("recipient", request.Recipient)
            .UsingJobData("templateName", request.TemplateName)
            .UsingJobData("cron", cronExpression)
            .Build();

        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity(jobId, PeriodicGroup)
            .WithCronSchedule(cronExpression, c => c.InTimeZone(TimeZoneInfo.Utc));

        if (startAt.HasValue) triggerBuilder = triggerBuilder.StartAt(startAt.Value);
        else triggerBuilder = triggerBuilder.StartNow();

        if (endAt.HasValue) triggerBuilder = triggerBuilder.EndAt(endAt.Value);

        await scheduler.ScheduleJob(jobDetail, triggerBuilder.Build(), ct);
        return jobId;
    }

    private static async Task<JobKey?> FindJobKeyAsync(IScheduler scheduler, string jobId, CancellationToken ct)
    {
        foreach (var group in new[] { OnceGroup, PeriodicGroup })
        {
            var key = new JobKey(jobId, group);
            if (await scheduler.CheckExists(key, ct)) return key;
        }
        return null;
    }

    private static async Task<object?> BuildJobDtoAsync(IScheduler scheduler, JobKey key, CancellationToken ct)
    {
        var jobDetail = await scheduler.GetJobDetail(key, ct);
        if (jobDetail is null) return null;

        var triggers = await scheduler.GetTriggersOfJob(key, ct);
        var trigger = triggers.FirstOrDefault();

        string? triggerState = null;
        DateTimeOffset? nextFireTime = null;
        DateTimeOffset? previousFireTime = null;
        string? cron = null;
        DateTimeOffset? endAt = null;

        if (trigger is not null)
        {
            var state = await scheduler.GetTriggerState(trigger.Key, ct);
            triggerState = state.ToString();
            nextFireTime = trigger.GetNextFireTimeUtc();
            previousFireTime = trigger.GetPreviousFireTimeUtc();
            endAt = trigger.EndTimeUtc;
            if (trigger is ICronTrigger cronTrigger)
                cron = cronTrigger.CronExpressionString;
        }

        var dataMap = jobDetail.JobDataMap;
        return new
        {
            jobId       = key.Name,
            type        = key.Group,
            tenantId    = dataMap.GetString("tenantId"),
            channel     = dataMap.GetString("channel"),
            recipient   = dataMap.GetString("recipient"),
            templateName = dataMap.GetString("templateName"),
            cron,
            nextFireTime,
            previousFireTime,
            endAt,
            status = triggerState
        };
    }

    private static NotificationRequest BuildRequest(
        string tenantId,
        NotificationChannel channel,
        string recipient,
        string? recipientName,
        string templateName,
        string language,
        Dictionary<string, object?> data,
        string? idempotencyKey,
        DateTimeOffset? scheduledAt)
    {
        return new NotificationRequest
        {
            TenantId      = tenantId,
            Channel       = channel,
            Recipient     = recipient,
            RecipientName = recipientName,
            TemplateName  = templateName,
            Language      = language,
            Data          = data,
            IdempotencyKey = idempotencyKey,
            ScheduledAt   = scheduledAt
        };
    }

    private static string SerializeRequest(NotificationRequest request) =>
        JsonSerializer.Serialize(request);
}
