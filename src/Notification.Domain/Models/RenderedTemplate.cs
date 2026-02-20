namespace Notification.Domain.Models;

/// <summary>
/// Output of the template renderer ready for a provider to send.
/// </summary>
public class RenderedTemplate
{
    /// <summary>Subject line (email) or notification title (push).</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Plain-text body.</summary>
    public string? TextBody { get; init; }

    /// <summary>HTML body (email only).</summary>
    public string? HtmlBody { get; init; }

    /// <summary>Raw data payload (push only, e.g. custom key/value pairs).</summary>
    public Dictionary<string, string> Data { get; init; } = new();
}
