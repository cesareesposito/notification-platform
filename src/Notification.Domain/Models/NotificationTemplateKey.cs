namespace Notification.Domain.Models;

/// <summary>
/// Uniquely identifies a notification template within a tenant and channel.
/// </summary>
public record NotificationTemplateKey(
    string ClientId,
    string TemplateName,
    NotificationChannel Channel,
    string Language = "en");
