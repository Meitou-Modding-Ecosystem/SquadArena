using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay;
using SquadArena;

namespace SquadRating;

/// <summary>
/// One template the bench rates.
/// </summary>
/// <param name="Id">The SQUAD_TEMPLATE.</param>
/// <param name="Name">Its name.</param>
/// <param name="Role">"guards" (a town's residents) or "patrol" (a town's roaming squads); "guards, patrol" when listed as both.</param>
sealed record BenchEntry(string Id, string Name, string Role)
{
    /// <summary>
    /// The entry as a contender: one roll of the template at squad size 1.
    /// </summary>
    public Contender Contender => new(new SideKey("template:" + Id, Name),
        new SideRequest { Name = Name, Members = [new SideMember { Template = Id }] });
}

/// <summary>
/// The bench's templates (docs/squad-rating.md "Bench"): every SQUAD_TEMPLATE a TOWN lists as <c>residents</c> (guards) or <c>roaming squads</c>
/// (patrols), leaving out traders and templates with nobody armed (shopkeepers, farmers, slaves).
/// </summary>
static class BenchList
{
    /// <summary>
    /// The templates, by name.
    /// </summary>
    public static IReadOnlyList<BenchEntry> Build(GameDatabase db)
    {
        var roles = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var town in db.OfType(FcsRecordType.Town).Select(TownData.From))
        {
            Note(roles, town.Residents, "guards");
            Note(roles, town.RoamingSquads, "patrol");
        }

        return [.. roles
            .Select(r => (Record: db.Find(r.Key), Roles: r.Value))
            .Where(r => r.Record is { Type: FcsRecordType.SquadTemplate } && Fights(db, SquadTemplate.From(r.Record)))
            .Select(r => new BenchEntry(r.Record!.StringId, r.Record.Name, string.Join(", ", r.Roles)))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Adds a role to each listed template.
    /// </summary>
    static void Note(Dictionary<string, SortedSet<string>> roles, IReadOnlyList<RecordLink> links, string role)
    {
        foreach (var link in links)
        {
            if (!roles.TryGetValue(link.Id, out var set)) roles[link.Id] = set = [];
            set.Add(role);
        }
    }

    /// <summary>
    /// Whether a template is a fighting squad: not a trader, and some character it can spawn carries a weapon or crossbow.
    /// </summary>
    static bool Fights(GameDatabase db, SquadTemplate template)
    {
        if (template.IsTrader) return false;

        var members = template.Squad.Concat(template.Squad2).Concat(template.ChooseFrom);
        if (template.Leader is { } leader) members = members.Append(leader);
        return members.Any(m => db.Find(m.Id) is { } c && (c.GetReferences("weapons").Count > 0 || c.GetReferences("crossbows").Count > 0));
    }
}
