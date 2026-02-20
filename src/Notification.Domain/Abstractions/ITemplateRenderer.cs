using Notification.Domain.Models;

namespace Notification.Domain.Abstractions;

/// <summary>
/// Renders a raw template string into a <see cref="RenderedTemplate"/>
/// by injecting the provided data context.
/// </summary>
public interface ITemplateRenderer
{
    Task<RenderedTemplate> RenderAsync(
        string rawTemplate,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default);
}
