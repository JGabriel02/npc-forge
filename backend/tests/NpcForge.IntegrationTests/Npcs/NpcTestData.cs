using System.Net.Http.Json;

namespace NpcForge.IntegrationTests.Npcs;

public static class NpcTestData
{
    /// <summary>Ficha válida; use <c>with</c> para variar um campo.</summary>
    public static StatBlockPayload ValidStatBlock() => new(
        Size: "Medium", CreatureType: "humanoid (dwarf)", Alignment: "lawful neutral",
        ArmorClass: 16, HitPoints: 26, HitDice: "4d8+8", Speed: "25 ft.",
        Str: 16, Dex: 10, Con: 14, Int: 10, Wis: 12, Cha: 8, ChallengeRating: "1",
        SavingThrows: null, Skills: "Atletismo +5", Senses: "visão no escuro 18 m", Languages: "Comum, Anão",
        Traits: [new FeaturePayload("Resiliência Anã", "Vantagem contra veneno.")],
        Actions: [new ActionPayload("Martelo", "Ataque corpo a corpo.", 5, "1d8+3")],
        Reactions: []);

    public static async Task<NpcBody> CreateNpcAsync(
        this HttpClient client, string name = "Borin", Guid? campaignId = null,
        string? race = null, string? occupation = null, StatBlockPayload? statBlock = null)
    {
        var response = await client.PostAsJsonAsync("/api/npcs", new { name, campaignId, race, occupation, statBlock });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NpcBody>())!;
    }

    public static async Task<Guid> CreateCampaignAsync(this HttpClient client, string name = "Campanha")
    {
        var response = await client.PostAsJsonAsync("/api/campaigns", new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>())!.Id;
    }

    private record IdBody(Guid Id);
}

public record FeaturePayload(string Name, string Description);

public record ActionPayload(string Name, string Description, int? AttackBonus, string? Damage);

public record StatBlockPayload(
    string Size, string CreatureType, string Alignment, int ArmorClass, int HitPoints, string HitDice, string Speed,
    int Str, int Dex, int Con, int Int, int Wis, int Cha, string ChallengeRating,
    string? SavingThrows, string? Skills, string? Senses, string? Languages,
    List<FeaturePayload> Traits, List<ActionPayload> Actions, List<FeaturePayload> Reactions);

public record ModifiersBody(int Str, int Dex, int Con, int Int, int Wis, int Cha);

public record StatBlockBody(
    string Size, int ArmorClass, int HitPoints, string HitDice, int Str, int Dex, string ChallengeRating,
    List<FeaturePayload> Traits, List<ActionPayload> Actions, List<FeaturePayload> Reactions,
    ModifiersBody Modifiers, int ProficiencyBonus);

public record NpcBody(
    Guid Id, Guid? CampaignId, string Name, string? Race, string? Occupation, string? Appearance,
    string? Personality, string? Background, string? Notes, StatBlockBody? StatBlock,
    DateTime CreatedAt, DateTime UpdatedAt);

public record NpcSummaryBody(
    Guid Id, string Name, string? Race, string? Occupation, Guid? CampaignId, string? CampaignName,
    bool HasStatBlock, string? ChallengeRating, DateTime UpdatedAt);

public record PagedBody<T>(List<T> Items, int Page, int PageSize, int TotalCount);
