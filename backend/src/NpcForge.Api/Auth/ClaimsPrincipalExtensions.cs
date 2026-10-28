using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace NpcForge.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id do usuário logado (claim <c>sub</c> do JWT).</summary>
    public static string GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(JwtRegisteredClaimNames.Sub)
           ?? throw new InvalidOperationException("Usuário autenticado sem claim 'sub'.");
}
