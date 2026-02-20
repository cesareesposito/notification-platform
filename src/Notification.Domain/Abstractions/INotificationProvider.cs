using Notification.Domain.Models;

namespace Notification.Domain.Abstractions;

/// <summary>
/// Common contract for all notification delivery providers
/// (SendGrid, SES, Firebase, APNs, WebPush, …).
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// Identifies the provider (e.g. "SendGrid", "Firebase").
    /// Used for tenant routing.
    /// </summary>
    string ProviderName { get; }

    /// <summary>The channel this provider handles.</summary>
    NotificationChannel Channel { get; }

    /// <summary>Send a rendered notification. Returns the provider-assigned message ID.</summary>
    Task<string?> SendAsync(
        NotificationRequest request,
        RenderedTemplate template,
        TenantConfig tenantConfig,
        CancellationToken cancellationToken = default);
}
