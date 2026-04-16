using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notification.Api.Requests.Auth;
using Notification.Persistence;
using Notification.Persistence.Entities;

namespace Notification.Api.Controllers;

/// <summary>
/// Issues JWT tokens for the admin UI.
/// - POST /api/auth/admin/login   → admin username/password → scope:admin token
/// - POST /api/auth/admin/exchange → raw API key (JSON body) → scope:client token
/// </summary>
[ApiController]
[Route("api/auth/admin")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly IConfiguration _config;

    public AuthController(IDbContextFactory<NotificationDbContext> dbFactory, IConfiguration config)
    {
        _dbFactory = dbFactory;
        _config = config;
    }

    /// <summary>Login with admin username and password.</summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var user = await db.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive, ct);

        if (user is null) return Unauthorized(new { error = "Invalid credentials." });

        var hasher = new PasswordHasher<AdminUserEntity>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid credentials." });

        var expiryHours = _config.GetValue<int>("JwtAuth:ExpiryAdminHours", 8);
        var token = BuildToken(
            [
                new Claim("scope", "admin"),
                new Claim("role", user.Role),
                new Claim("username", user.Username)
            ],
            TimeSpan.FromHours(expiryHours));

        return Ok(new { token, scope = "admin", expiresIn = (int)TimeSpan.FromHours(expiryHours).TotalSeconds });
    }

    /// <summary>Exchange a raw API key for a scoped client JWT. The key is passed in the JSON body (never in the URL).</summary>
    [HttpPost("exchange")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Exchange([FromBody] ApiKeyExchangeRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        var keyHash = ComputeSha256(req.ApiKey);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ApiKeyHash == keyHash && t.IsActive, ct);

        if (tenant is null)
            return Unauthorized(new { error = "Invalid or revoked API key." });

        var clientId = tenant.ClientId;
        var clientName = tenant.DisplayName;

        var expiryHours = _config.GetValue<int>("JwtAuth:ExpiryClientHours", 1);
        var token = BuildToken(
            [
                new Claim("scope", "client"),
                new Claim("clientName", clientName),
                new Claim("notificationClientId", clientId)
            ],
            TimeSpan.FromHours(expiryHours));

        return Ok(new
        {
            token,
            scope = "client",
            clientName,
            clientId,
            expiresIn = (int)TimeSpan.FromHours(expiryHours).TotalSeconds
        });
    }

    private string BuildToken(IEnumerable<Claim> claims, TimeSpan expiry)
    {
        var secret = _config["JwtAuth:Secret"]
            ?? throw new InvalidOperationException("JwtAuth:Secret is not configured.");
        var issuer = _config["JwtAuth:Issuer"] ?? "notification-platform";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    internal static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
