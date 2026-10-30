using System.ComponentModel.DataAnnotations;

namespace NpcForge.Api.Auth;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    [Required, MaxLength(50)] string DisplayName);

public record LoginRequest([Required] string Email, [Required] string Password);

public record LoginResponse(string Token, DateTime ExpiresAt);

public record MeResponse(string Id, string Email, string DisplayName, bool IsDemo);
