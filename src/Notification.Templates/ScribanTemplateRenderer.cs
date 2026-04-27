using Notification.Domain.Abstractions;
using Notification.Domain.Models;
using Scriban;
using Scriban.Runtime;
using System.Text.RegularExpressions;

namespace Notification.Templates;

/// <summary>
/// Renders templates using the Scriban engine.
/// Template format uses {{ variable }} syntax.
/// A template file may contain an optional front-matter block delimited by ---
/// to set the subject and any extra fields:
///
///   ---
///   subject: Welcome, {{ recipient_name }}!
///   ---
///   &lt;html&gt;…&lt;/html&gt;
///
/// If no front-matter is present the first non-blank line is used as the subject.
/// </summary>
public class ScribanTemplateRenderer : ITemplateRenderer
{
    public async Task<RenderedTemplate> RenderAsync(
        string rawTemplate,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        var (subjectRaw, bodyRaw) = SplitFrontMatter(rawTemplate);

        var scriptObject = new ScriptObject();
        foreach (var (key, value) in data)
            scriptObject.Add(key, value);

        var context = new TemplateContext { StrictVariables = false };
        context.PushGlobal(scriptObject);

        var subject = await RenderStringAsync(subjectRaw, context);
        var body = await RenderStringAsync(bodyRaw, context);

        var isHtml = LooksLikeHtml(body);

        return new RenderedTemplate
        {
            Subject = subject.Trim(),
            HtmlBody = isHtml ? body : null,
            TextBody = isHtml ? StripHtml(body) : body
        };
    }

    private static (string subject, string body) SplitFrontMatter(string raw)
    {
        const string delimiter = "---";
        var lines = raw.Split('\n');

        if (lines.Length > 2 && lines[0].Trim() == delimiter)
        {
            var endIndex = Array.FindIndex(lines, 1, l => l.Trim() == delimiter);
            if (endIndex > 0)
            {
                var frontMatter = lines[1..endIndex];
                var body = string.Join('\n', lines[(endIndex + 1)..]);
                var subject = frontMatter
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.StartsWith("subject:", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("subject:".Length).Trim() ?? string.Empty;
                return (subject, body);
            }
        }

        // No front-matter: first non-blank line = subject, rest = body
        var firstNonBlank = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (firstNonBlank < 0) return (string.Empty, raw);

        var subjectLine = lines[firstNonBlank].Trim();
        var bodyLines = string.Join('\n', lines[(firstNonBlank + 1)..]);
        return (subjectLine, bodyLines);
    }

    private static async Task<string> RenderStringAsync(string template, TemplateContext context)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        var parsed = Template.Parse(template);
        return await parsed.RenderAsync(context);
    }

    private static string StripHtml(string html)
    {
        // Very naive strip for plain-text fallback
        var result = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(result, @"\s{2,}", " ").Trim();
    }

    private static bool LooksLikeHtml(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        return Regex.IsMatch(body, @"<\s*/?\s*[a-zA-Z][^>]*>", RegexOptions.CultureInvariant);
    }
}
