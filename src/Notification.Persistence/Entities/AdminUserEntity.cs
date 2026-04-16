namespace Notification.Persistence.Entities;

/// <summary>
/// Admin user who can log in to the notification-platform admin UI with username/password.
/// Password is hashed with ASP.NET Core <c>PasswordHasher&lt;AdminUserEntity&gt;</c>.
/// </summary>
public class AdminUserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;

    /// <summary>BCrypt-style hash produced by ASP.NET Core PasswordHasher.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>"admin" or "readonly".</summary>
    public string Role { get; set; } = "admin";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
}
