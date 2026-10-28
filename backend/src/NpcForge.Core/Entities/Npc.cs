namespace NpcForge.Core.Entities;

public class Npc
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";

    /// <summary><c>null</c> = "Sem campanha".</summary>
    public Guid? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }

    public string Name { get; set; } = "";
    public string? Race { get; set; }
    public string? Occupation { get; set; }
    public string? Appearance { get; set; }
    public string? Personality { get; set; }
    public string? Background { get; set; }

    /// <summary>Anotações privadas do mestre.</summary>
    public string? Notes { get; set; }

    /// <summary><c>null</c> = NPC só narrativo, sem ficha de combate.</summary>
    public StatBlock? StatBlock { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
