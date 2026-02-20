using System.ComponentModel.DataAnnotations;
using Notification.Domain.Models;

namespace Notification.Api.Requests.Admin;

public class UpsertTemplateRequest
{
    [Required, MaxLength(200)] public string TemplateName { get; init; } = string.Empty;
    [Required] public NotificationChannel Channel { get; init; }
    [MaxLength(10)] public string Language { get; init; } = "en";
    [Required] public string Content { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}
