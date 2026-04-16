using System.ComponentModel.DataAnnotations;

namespace Notification.Api.Requests.Auth;

public class AdminLoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class ApiKeyExchangeRequest
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;
}
