using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NpcForge.Api.Auth;
using NpcForge.Core.Common;
using NpcForge.Core.Npcs;

namespace NpcForge.Api.Controllers;

[ApiController]
[Route("api/npcs")]
public class NpcsController(
    INpcService npcs, INpcTransferService transfer, TimeProvider time, IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<NpcSummaryDto>> List([FromQuery] NpcQuery query, CancellationToken ct)
        => npcs.ListAsync(User.GetUserId(), query, ct);

    /// <summary>Baixa um arquivo com todos os NPCs dos filtros da lista (page/pageSize são ignorados).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] NpcQuery filters, CancellationToken ct)
    {
        // Nunca gera um arquivo que a importação recusaria.
        const string split = "Filtre por campanha ou tipo e exporte em partes.";
        var file = await transfer.ExportAsync(User.GetUserId(), filters, ct);
        if (file.Npcs!.Count > NpcExportFile.MaxNpcs)
            return Problem(
                $"Esse filtro tem {file.Npcs.Count} NPCs, e um arquivo leva no máximo {NpcExportFile.MaxNpcs}. {split}",
                statusCode: StatusCodes.Status400BadRequest);

        // Serializa aqui (com as mesmas opções do MVC) para medir o tamanho antes de enviar.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, jsonOptions.Value.JsonSerializerOptions);
        if (bytes.Length > NpcExportFile.MaxFileBytes)
            return Problem(
                $"O arquivo passaria de {NpcExportFile.MaxFileBytes / 1024 / 1024} MB. {split}",
                statusCode: StatusCodes.Status400BadRequest);

        SetAttachment(FileNames.ForList(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime)));
        return File(bytes, "application/json");
    }

    [HttpGet("{id:guid}/export")]
    public async Task<ActionResult<NpcExportFile>> ExportOne(Guid id, CancellationToken ct)
    {
        var file = await transfer.ExportOneAsync(User.GetUserId(), id, ct);
        if (file is null) return NotFound();

        SetAttachment(FileNames.ForNpc(file.Npcs![0].Name));
        return file;
    }

    /// <summary>Importa um arquivo de exportação: tudo ou nada, NPCs sem campanha.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(NpcExportFile.MaxFileBytes)]
    public async Task<IActionResult> Import(NpcExportFile file, CancellationToken ct)
    {
        var imported = await transfer.ImportAsync(User.GetUserId(), file, ct);
        return StatusCode(StatusCodes.Status201Created, new { imported });
    }

    // "attachment" faz o navegador baixar o JSON como arquivo, com este nome.
    private void SetAttachment(string fileName)
        => Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileName }.ToString();

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
