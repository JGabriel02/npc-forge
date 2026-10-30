using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpcForge.Core.Entities;
using NpcForge.Core.Npcs;
using NpcForge.Infrastructure.Identity;
using NpcForge.Infrastructure.Persistence;
using NpcForge.Infrastructure.Services;

namespace NpcForge.Infrastructure.Demo;

/// <summary>
/// Cria a conta de visitante (sem senha) com campanhas e NPCs de exemplo, na primeira vez que alguém
/// entra como visitante. Depois disso, só devolve a conta que já existe.
/// </summary>
public class DemoSeeder(AppDbContext db, UserManager<AppUser> users, TimeProvider time)
{
    public const string Email = "visitante@npcforge.demo";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public async Task<AppUser> EnsureSeededAsync(CancellationToken ct)
    {
        if (await users.FindByEmailAsync(Email) is { } existing) return existing;

        // Conta e conteúdo na mesma transação: ninguém vê uma conta demo pela metade.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var user = new AppUser { UserName = Email, Email = Email, DisplayName = "Visitante", EmailConfirmed = true };
            var created = await users.CreateAsync(user); // sem senha: só se entra por /api/auth/demo
            if (!created.Succeeded)
            {
                // Outra requisição criou a conta antes (e-mail já visível como duplicado).
                if (created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                    return await ExistingAfterRaceAsync(transaction, ct);
                throw new InvalidOperationException(
                    $"Não foi possível criar a conta demo: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }

            AddContent(user.Id);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return user;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dois primeiros cliques ao mesmo tempo: o índice único barra o segundo, que usa a conta do primeiro.
            return await ExistingAfterRaceAsync(transaction, ct);
        }
    }

    /// <summary>O conteúdo da demo: um arquivo de exportação com o campo extra "campaign" em cada NPC.</summary>
    public static string ReadSeedFile()
    {
        using var stream = typeof(DemoSeeder).Assembly.GetManifestResourceStream("NpcForge.Infrastructure.Demo.demo-npcs.json")
            ?? throw new InvalidOperationException("Recurso demo-npcs.json não encontrado.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void AddContent(string userId)
    {
        var text = ReadSeedFile();
        var file = JsonSerializer.Deserialize<NpcExportFile>(text, Json)!;
        var campaignNames = JsonSerializer.Deserialize<SeedCampaigns>(text, Json)!.Npcs.Select(n => n.Campaign).ToList();
        var now = time.GetUtcNow().UtcDateTime;

        var campaigns = campaignNames.OfType<string>().Distinct().ToDictionary(
            name => name,
            name => new Campaign { Id = Guid.NewGuid(), UserId = userId, Name = name, CreatedAt = now, UpdatedAt = now });
        db.Campaigns.AddRange(campaigns.Values);

        for (var i = 0; i < file.Npcs!.Count; i++)
        {
            var campaignId = campaignNames[i] is { } name ? campaigns[name].Id : (Guid?)null;
            db.Npcs.Add(file.Npcs[i].ToNewNpc(userId, campaignId, now));
        }
    }

    private async Task<AppUser> ExistingAfterRaceAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken ct)
    {
        await transaction.RollbackAsync(ct);
        db.ChangeTracker.Clear();
        return await users.FindByEmailAsync(Email)
            ?? throw new InvalidOperationException("A conta demo deveria existir depois de um conflito de criação.");
    }

    private record SeedCampaigns(List<SeedNpc> Npcs);

    private record SeedNpc(string? Campaign);
}
