using System.Collections.Concurrent;
using NpcForge.Core.Ai;

namespace NpcForge.IntegrationTests.Ai;

/// <summary>IA de mentira para os testes: devolve respostas enfileiradas (ou uma resposta válida padrão).</summary>
public class FakeAiProvider : IAiProvider
{
    public const string DefaultJson = """
        {
          "name": "Vasilka", "race": "humana", "occupation": "costureira",
          "appearance": "Olhos de vidro e sorriso costurado.", "personality": "Educada demais.",
          "background": "Foi montada por um cientista louco.",
          "statBlock": {
            "size": "Medium", "creatureType": "constructo", "alignment": "neutra", "armorClass": 13,
            "hitPoints": 22, "hitDice": "4d8 + 4", "speed": "9 m",
            "str": 14, "dex": 12, "con": 12, "int": 10, "wis": 10, "cha": 12,
            "challengeRating": "1", "savingThrows": null, "skills": null, "senses": null, "languages": "Comum",
            "traits": [], "actions": [{ "name": "Pancada", "description": "Corpo a corpo.", "attackBonus": 4, "damage": "1d6 + 2 Contundente" }],
            "reactions": []
          }
        }
        """;

    private readonly ConcurrentQueue<Func<CancellationToken, Task<string>>> _responses = new();

    public ConcurrentQueue<AiPrompt> Calls { get; } = new();

    public void Reset()
    {
        _responses.Clear();
        Calls.Clear();
    }

    public void Enqueue(string response) => _responses.Enqueue(_ => Task.FromResult(response));

    public void EnqueueFailure(Exception exception) => _responses.Enqueue(_ => Task.FromException<string>(exception));

    public void EnqueueDelay(TimeSpan delay) => _responses.Enqueue(async ct =>
    {
        await Task.Delay(delay, ct);
        return DefaultJson;
    });

    public Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken ct)
    {
        Calls.Enqueue(new AiPrompt(systemPrompt, userPrompt, jsonSchema));
        return _responses.TryDequeue(out var next) ? next(ct) : Task.FromResult(DefaultJson);
    }
}
