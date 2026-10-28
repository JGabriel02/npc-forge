namespace NpcForge.Core.Entities;

public class Campaign
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>Contexto da campanha (tom, cenário) enviado para a IA.</summary>
    public string? Setting { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Npc> Npcs { get; set; } = [];
}
