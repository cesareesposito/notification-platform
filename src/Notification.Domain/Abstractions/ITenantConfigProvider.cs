using Notification.Domain.Models;

namespace Notification.Domain.Abstractions;

/// <summary>
/// Resolves per-tenant configuration at runtime.
/// </summary>
public interface ITenantConfigProvider
{
    Task<TenantConfig?> GetConfigAsync(
    string clientId,
        CancellationToken cancellationToken = default);
}
