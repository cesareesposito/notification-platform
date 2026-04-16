using Microsoft.Extensions.Options;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;

// Use an alias to avoid collision between the 'TenantConfig' namespace and 'TenantConfig' class
using TenantConfigModel = Notification.Domain.Models.TenantConfig;

namespace Notification.Infrastructure.Tenants;

/// <summary>
/// In-process tenant config provider backed by appsettings / Options.
/// Replace or complement with a DB-backed provider as needed.
/// </summary>
public class StaticTenantConfigProvider : ITenantConfigProvider
{
    private readonly Dictionary<string, TenantConfigModel> _configs;

    public StaticTenantConfigProvider(IOptions<TenantConfigOptions> options)
    {
        _configs = options.Value.Tenants
            .ToDictionary(t => t.ClientId, StringComparer.OrdinalIgnoreCase);
    }

    public Task<TenantConfigModel?> GetConfigAsync(string clientId, CancellationToken cancellationToken = default)
    {
        _configs.TryGetValue(clientId, out var config);
        return Task.FromResult(config);
    }
}

public class TenantConfigOptions
{
    public const string SectionName = "Tenants";
    public List<TenantConfigModel> Tenants { get; init; } = new();
}
