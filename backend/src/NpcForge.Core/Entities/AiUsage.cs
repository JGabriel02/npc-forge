namespace NpcForge.Core.Entities;

/// <summary>Quantas gerações de NPC por IA um usuário fez em um dia (UTC).</summary>
public class AiUsage
{
    public string UserId { get; set; } = "";
    public DateOnly Date { get; set; }
    public int GenerationCount { get; set; }
}
