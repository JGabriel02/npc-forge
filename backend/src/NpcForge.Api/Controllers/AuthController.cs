using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpcForge.Api.Auth;
using NpcForge.Api.RateLimiting;
using NpcForge.Infrastructure.Identity;

namespace NpcForge.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> users, JwtTokenService tokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimit.Policy)]
    public async Task<ActionResult<MeResponse>> Register(RegisterRequest request)
    {
        var user = new AppUser { UserName = request.Email, Email = request.Email, DisplayName = request.DisplayName };
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dois cadastros simultâneos passam pela checagem do Identity; o índice único do banco barra o segundo.
            ModelState.AddModelError("DuplicateEmail", "Este e-mail já está cadastrado.");
            return ValidationProblem(ModelState);
        }

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(Me), null, ToMe(user));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimit.Policy)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Calcula um hash mesmo assim, para o tempo de resposta não revelar se o e-mail existe.
            users.PasswordHasher.HashPassword(new AppUser(), request.Password);
            return InvalidCredentials();
        }

        if (await users.IsLockedOutAsync(user))
            return Problem(statusCode: StatusCodes.Status429TooManyRequests,
                title: "Muitas tentativas de login. Tente novamente em alguns minutos.");

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return InvalidCredentials();
        }

        await users.ResetAccessFailedCountAsync(user);
        var (token, expiresAt) = tokens.CreateToken(user);
        return new LoginResponse(token, expiresAt);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var user = await users.FindByIdAsync(User.GetUserId());
        return user is null ? Unauthorized() : ToMe(user);
    }

    private ObjectResult InvalidCredentials()
        => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha inválidos.");

    private static MeResponse ToMe(AppUser user) => new(user.Id, user.Email!, user.DisplayName);
}
