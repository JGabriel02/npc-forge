using System.Text.Json.Nodes;

namespace NpcForge.UnitTests.Ai;

/// <summary>Respostas de exemplo do modelo para os testes do validador.</summary>
public static class TestDrafts
{
    public const string ValidJson = """
        {
          "name": "Borin Martelo-de-Pedra",
          "race": "anão",
          "occupation": "ferreiro",
          "appearance": "Barba ruiva trançada e braços cheios de cicatrizes de forja.",
          "personality": "Rabugento, mas leal a quem paga em dia.",
          "background": "Perdeu a forja da família num incêndio e jurou reconstruí-la.",
          "statBlock": {
            "size": "Medium",
            "creatureType": "humanoide (anão)",
            "alignment": "leal e neutro",
            "armorClass": 16,
            "hitPoints": 26,
            "hitDice": "4d8 + 8",
            "speed": "7,5 m",
            "str": 16, "dex": 10, "con": 14, "int": 10, "wis": 12, "cha": 8,
            "challengeRating": "1",
            "savingThrows": null,
            "skills": "Atletismo +5",
            "senses": "Visão no Escuro 18 m",
            "languages": "Comum, Anão",
            "traits": [{ "name": "Resiliência Anã", "description": "Vantagem contra veneno." }],
            "actions": [{ "name": "Martelo", "description": "Ataque corpo a corpo.", "attackBonus": 5, "damage": "1d8 + 3 Contundente" }],
            "reactions": []
          }
        }
        """;

    /// <summary>O JSON válido com uma alteração aplicada (para testar um problema por vez).</summary>
    public static string With(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(ValidJson)!.AsObject();
        change(root);
        return root.ToJsonString();
    }

    public static JsonObject StatBlock(this JsonObject root) => root["statBlock"]!.AsObject();
}
