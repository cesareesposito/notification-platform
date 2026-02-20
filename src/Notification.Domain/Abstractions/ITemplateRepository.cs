using Notification.Domain.Models;

namespace Notification.Domain.Abstractions;

/// <summary>
/// Loads raw template content from a backing store
/// (filesystem, GCS, database, …).
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Returns the raw template string for the given key,
    /// or null if the template is not found.
    /// </summary>
    Task<string?> GetTemplateAsync(
        NotificationTemplateKey key,
        CancellationToken cancellationToken = default);
}
