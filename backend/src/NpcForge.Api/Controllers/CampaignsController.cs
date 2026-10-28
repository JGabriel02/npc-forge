using Microsoft.AspNetCore.Mvc;
using NpcForge.Api.Auth;
using NpcForge.Core.Campaigns;

namespace NpcForge.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
public class CampaignsController(ICampaignService campaigns) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CampaignDto>> List(CancellationToken ct) => campaigns.ListAsync(User.GetUserId(), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> Get(Guid id, CancellationToken ct)
        => await campaigns.GetAsync(User.GetUserId(), id, ct) is { } campaign ? campaign : NotFound();

    [HttpPost]
    public async Task<ActionResult<CampaignDto>> Create(CampaignRequest request, CancellationToken ct)
    {
        var campaign = await campaigns.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = campaign.Id }, campaign);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> Update(Guid id, CampaignRequest request, CancellationToken ct)
        => await campaigns.UpdateAsync(User.GetUserId(), id, request, ct) is { } campaign ? campaign : NotFound();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await campaigns.DeleteAsync(User.GetUserId(), id, ct) ? NoContent() : NotFound();
}
