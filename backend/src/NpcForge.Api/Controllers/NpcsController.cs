using Microsoft.AspNetCore.Mvc;
using NpcForge.Api.Auth;
using NpcForge.Core.Common;
using NpcForge.Core.Npcs;

namespace NpcForge.Api.Controllers;

[ApiController]
[Route("api/npcs")]
public class NpcsController(INpcService npcs) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<NpcSummaryDto>> List([FromQuery] NpcQuery query, CancellationToken ct)
        => npcs.ListAsync(User.GetUserId(), query, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NpcDto>> Get(Guid id, CancellationToken ct)
        => await npcs.GetAsync(User.GetUserId(), id, ct) is { } npc ? npc : NotFound();

    [HttpPost]
    public async Task<ActionResult<NpcDto>> Create(CreateNpcRequest request, CancellationToken ct)
    {
        var result = await npcs.CreateAsync(User.GetUserId(), request, ct);
        return result.Error is null
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : ToError(result.Error.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NpcDto>> Update(Guid id, UpdateNpcRequest request, CancellationToken ct)
    {
        var result = await npcs.UpdateAsync(User.GetUserId(), id, request, ct);
        return result.Error is null ? result.Value! : ToError(result.Error.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await npcs.DeleteAsync(User.GetUserId(), id, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/statblock")]
    public async Task<ActionResult<NpcDto>> SetStatBlock(Guid id, StatBlockRequest request, CancellationToken ct)
        => await npcs.SetStatBlockAsync(User.GetUserId(), id, request, ct) is { } npc ? npc : NotFound();

    [HttpDelete("{id:guid}/statblock")]
    public async Task<ActionResult<NpcDto>> RemoveStatBlock(Guid id, CancellationToken ct)
        => await npcs.RemoveStatBlockAsync(User.GetUserId(), id, ct) is { } npc ? npc : NotFound();

    private ActionResult ToError(ServiceError error)
    {
        if (error == ServiceError.NotFound)
            return NotFound();

        ModelState.AddModelError("campaignId", "Campanha não encontrada.");
        return ValidationProblem(ModelState);
    }
}
