using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NpcForge.Core.Ai;

namespace NpcForge.Api.Controllers;

public record FeaturesDto(bool Ai);

/// <summary>Diz ao frontend quais funcionalidades opcionais estão ligadas nesta instalação.</summary>
[ApiController]
[Route("api/features")]
[AllowAnonymous]
public class FeaturesController(IOptions<AiOptions> ai) : ControllerBase
{
    [HttpGet]
    public FeaturesDto Get() => new(ai.Value.Enabled);
}
