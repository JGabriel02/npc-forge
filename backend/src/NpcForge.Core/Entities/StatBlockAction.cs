namespace NpcForge.Core.Entities;

/// <summary>Uma ação da ficha; ataques têm bônus e dano.</summary>
public class StatBlockAction : StatBlockFeature
{
    public int? AttackBonus { get; set; }
    public string? Damage { get; set; }
}
