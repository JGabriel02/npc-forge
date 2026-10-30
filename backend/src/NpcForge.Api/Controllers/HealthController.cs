using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpcForge.Infrastructure.Persistence;

namespace NpcForge.Api.Controllers;

/// <summary>Diz se a API está de pé e alcança o banco. Usada pelo smoke test do deploy.</summary>
[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => await db.Database.CanConnectAsync(ct)
            ? Ok(new { status = "ok" })
            : Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "O banco de dados não respondeu.");
}
