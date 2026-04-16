using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Domain.Abstractions;
using Notification.Domain.Models;

namespace Notification.Templates;

/// <summary>
/// Loads templates from the local filesystem.
/// File layout: {BasePath}/{clientId}/{channel}/{language}/{templateName}.scriban
///
/// Falls back to the "default" client folder if a client-specific template is not found.
/// </summary>
public class FileSystemTemplateRepository : ITemplateRepository
{
    private readonly FileSystemTemplateOptions _options;
    private readonly ILogger<FileSystemTemplateRepository> _logger;

    public FileSystemTemplateRepository(
        IOptions<FileSystemTemplateOptions> options,
        ILogger<FileSystemTemplateRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetTemplateAsync(
        NotificationTemplateKey key,
        CancellationToken cancellationToken = default)
    {
        // Try tenant-specific first, then fallback to "default"
        var candidates = new[]
        {
            BuildPath(key.ClientId, key.Channel, key.Language, key.TemplateName),
            BuildPath(key.ClientId, key.Channel, "en", key.TemplateName),       // lang fallback
            BuildPath("default", key.Channel, key.Language, key.TemplateName),  // client fallback
            BuildPath("default", key.Channel, "en", key.TemplateName)
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;

            _logger.LogDebug("Loading template from {Path}", path);
            return await File.ReadAllTextAsync(path, cancellationToken);
        }

        _logger.LogWarning(
            "Template not found for Client={ClientId} Channel={Channel} Lang={Language} Name={Name}",
            key.ClientId, key.Channel, key.Language, key.TemplateName);

        return null;
    }

    private string BuildPath(string clientId, NotificationChannel channel, string lang, string name)
        => Path.Combine(
            _options.BasePath,
            clientId,
            channel.ToString().ToLowerInvariant(),
            lang,
            $"{name}.scriban");
}

public class FileSystemTemplateOptions
{
    public const string SectionName = "Templates:FileSystem";
    public string BasePath { get; init; } = "templates";
}
