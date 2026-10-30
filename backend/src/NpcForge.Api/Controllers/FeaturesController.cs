using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NpcForge.Core.Ai;
using NpcForge.Core.Demo;

namespace NpcForge.Api.Controllers;

public record FeaturesDto(bool Ai, bool Demo);

/// <summary>Diz ao frontend quais funcionalidades opcionais estão ligadas nesta instalação.</summary>
[ApiController]
[Route("api/features")]
[AllowAnonymous]
public class FeaturesController(IOptions<AiOptions> ai, IOptions<DemoOptions> demo) : ControllerBase
{
    [HttpGet]
    public FeaturesDto Get() => new(ai.Value.Enabled, demo.Value.Enabled);
}
