namespace NpcForge.Core.Npcs;

public class NpcQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Um GUID de campanha, ou <c>"none"</c> para NPCs sem campanha.</summary>
    public string? CampaignId { get; set; }

    /// <summary>Busca em nome, raça e profissão (sem diferenciar maiúsculas).</summary>
    public string? Search { get; set; }

    public bool? HasStatBlock { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
}
