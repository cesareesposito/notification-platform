using System.ComponentModel.DataAnnotations;

namespace Notification.Api.Requests;

public class PeriodicEmailRequest
{
    [Required] public string ClientId { get; init; } = string.Empty;
    [Required, EmailAddress] public string Recipient { get; init; } = string.Empty;
    public string? RecipientName { get; init; }
    [Required] public string TemplateName { get; init; } = string.Empty;
    public string Language { get; init; } = "en";
    public Dictionary<string, object?> Data { get; init; } = new();
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Quartz cron expression (6 fields: sec min hour day month weekday).
    /// Examples:
    ///   "0 0 9 * * ?"      — every day at 09:00
    ///   "0 30 8 ? * MON"   — every Monday at 08:30
    ///   "0 0/10 * * * ?"   — every 10 minutes
    /// </summary>
    [Required] public string CronExpression { get; init; } = string.Empty;

    /// <summary>Time zone used to interpret the cron expression. Null or empty = UTC.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>When to start firing. Null = immediately.</summary>
    public DateTimeOffset? StartAt { get; init; }

    /// <summary>When to stop firing. Null = never.</summary>
    public DateTimeOffset? EndAt { get; init; }
}
