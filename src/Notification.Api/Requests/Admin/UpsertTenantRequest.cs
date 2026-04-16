using System.ComponentModel.DataAnnotations;

namespace Notification.Api.Requests.Admin;

public class UpsertTenantRequest
{
    [Required, MaxLength(200)] public string DisplayName { get; init; } = string.Empty;

    [Required, MaxLength(50)] public string EmailProvider { get; init; } = "smtp";
    [EmailAddress] public string? EmailFrom { get; init; }
    public string? EmailFromName { get; init; }

    [Required, MaxLength(50)] public string PushProvider { get; init; } = "Firebase";

    public Dictionary<string, string> ProviderSettings { get; init; } = new();

    [Range(1, 10000)] public int RateLimitPerMinute { get; init; } = 100;
    public bool IsActive { get; init; } = true;
}
