using System.ComponentModel.DataAnnotations;

namespace NpcForge.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    [Required, MinLength(32)]
    public string Key { get; set; } = "";

    public string Issuer { get; set; } = "NpcForge";
    public string Audience { get; set; } = "NpcForge";
    public int ExpiresMinutes { get; set; } = 1440;
}
