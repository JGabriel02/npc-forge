namespace NpcForge.Core.Entities;

/// <summary>Ficha de combate D&D 5e. Valores derivados (modificadores, proficiência) não são salvos.</summary>
public class StatBlock
{
    public Guid NpcId { get; set; }

    public CreatureSize Size { get; set; }
    public string CreatureType { get; set; } = "";
    public string Alignment { get; set; } = "";
    public int ArmorClass { get; set; }
    public int HitPoints { get; set; }
    public string HitDice { get; set; } = "";
    public string Speed { get; set; } = "";

    public int Str { get; set; }
    public int Dex { get; set; }
    public int Con { get; set; }
    public int Int { get; set; }
    public int Wis { get; set; }
    public int Cha { get; set; }

    public string ChallengeRating { get; set; } = "";
    public string? SavingThrows { get; set; }
    public string? Skills { get; set; }
    public string? Senses { get; set; }
    public string? Languages { get; set; }

    public List<StatBlockFeature> Traits { get; set; } = [];
    public List<StatBlockAction> Actions { get; set; } = [];
    public List<StatBlockFeature> Reactions { get; set; } = [];
}
