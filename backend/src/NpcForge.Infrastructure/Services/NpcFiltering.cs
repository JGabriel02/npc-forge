using Microsoft.EntityFrameworkCore;
using NpcForge.Core.Entities;
using NpcForge.Core.Npcs;

namespace NpcForge.Infrastructure.Services;

/// <summary>Filtros da lista de NPCs, compartilhados pela listagem e pela exportação.</summary>
public static class NpcFiltering
{
    // '%' e '_' são curingas no ILIKE; o Npgsql não define escape por padrão, então passamos '\' explicitamente.
    private const string LikeEscape = @"\";

    public static IQueryable<Npc> Apply(IQueryable<Npc> npcs, NpcQuery query)
    {
        if (query.CampaignId == "none")
            npcs = npcs.Where(n => n.CampaignId == null);
        else if (query.CampaignId is not null)
            npcs = Guid.TryParse(query.CampaignId, out var campaignId)
                ? npcs.Where(n => n.CampaignId == campaignId)
                : npcs.Where(_ => false); // id de campanha inválido: nenhum resultado

        if (query.HasStatBlock is { } hasStatBlock)
            npcs = npcs.Where(n => (n.StatBlock != null) == hasStatBlock);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            npcs = npcs.Where(n => EF.Functions.ILike(n.Name, pattern, LikeEscape)
                                   || (n.Race != null && EF.Functions.ILike(n.Race, pattern, LikeEscape))
                                   || (n.Occupation != null && EF.Functions.ILike(n.Occupation, pattern, LikeEscape)));
        }

        return npcs;
    }

    private static string EscapeLike(string value)
        => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
