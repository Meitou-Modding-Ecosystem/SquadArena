using System.Numerics;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay;
using Meitou.Simulation;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Population;

namespace SquadArena;

/// <summary>
/// Puts a request's two sides into the world (docs/squad-rating.md "How a fight runs"): each fighter alone in an idle squad of its side's arena
/// faction, side 0 south of the point and side 1 north, ranks of up to <see cref="RankWidth"/> facing each other <see cref="Gap"/> apart; then queues
/// each fighter's loadout and stats for the next tick, before any order is given.
/// </summary>
sealed class FightSpawner(PopulationSystem population, GameDatabase db)
{
    /// <summary>
    /// The two fronts start this far apart (the game's own --battle uses the same).
    /// </summary>
    public const float Gap = 40;

    /// <summary>
    /// Fighters stand this far apart in a rank.
    /// </summary>
    public const float Spacing = 6;

    /// <summary>
    /// The most fighters in one rank.
    /// </summary>
    public const int RankWidth = 10;

    /// <summary>
    /// The planned fighters of each side, or the reason the request can't be fought.
    /// </summary>
    public (List<SideMember>[] Sides, string? Error) Plan(FightRequest request)
    {
        if (request.Sides is not { Length: 2 }) return ([], "a fight needs exactly two sides");
        if (request.At is not { Length: 2 } at || !float.IsFinite(at[0]) || !float.IsFinite(at[1])) return ([], "'at' needs two numbers");

        var sides = new List<SideMember>[2];
        for (int side = 0; side < 2; side++)
        {
            sides[side] = [];
            foreach (var line in request.Sides[side].Members)
            {
                if (Expand(line, request.Seed, (ulong)(side * 1000 + sides[side].Count), sides[side]) is { } error)
                    return ([], $"side {side}: {error}");
            }

            if (sides[side].Count == 0) return ([], $"side {side} has nobody to fight");
        }

        return (sides, null);
    }

    /// <summary>
    /// Spawns the planned fighters and queues their kit; returns each side's ids in plan order.
    /// </summary>
    public List<CharacterId>[] Spawn(World world, FightRequest request, List<SideMember>[] plan, int[] factions)
    {
        var centre = new Vector2(request.At[0], request.At[1]);
        var spawned = new List<CharacterId>[2];
        for (int side = 0; side < 2; side++)
        {
            spawned[side] = [];
            var offsets = Offsets(plan[side].Count, side);
            for (int i = 0; i < plan[side].Count; i++)
            {
                var member = plan[side][i];
                int variant = (int)(request.Seed % 100_000) * 4 + side * 1000 + i + 1;
                var id = population.SpawnSandbox(world, member.Character!, centre + offsets[i], player: false, yaw: side == 0 ? 0 : MathF.PI,
                    variant: variant, faction: factions[side], race: member.Race);
                spawned[side].Add(id);
                Outfit(world, id, member);
            }
        }

        return spawned;
    }

    /// <summary>
    /// Each fighter's spot from the centre: ranks of <see cref="RankWidth"/>, the front rank <see cref="Gap"/> / 2 from the middle, side 0 at −Z.
    /// </summary>
    static Vector2[] Offsets(int count, int side)
    {
        float sign = side == 0 ? -1 : 1;
        var offsets = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            int rank = i / RankWidth, first = rank * RankWidth, inRank = Math.Min(RankWidth, count - first);
            offsets[i] = new Vector2((i - first - (inRank - 1) / 2f) * Spacing, sign * (Gap / 2 + rank * Spacing));
        }

        return offsets;
    }

    #region Rosters

    /// <summary>
    /// Adds a line's fighters to <paramref name="into"/>: <see cref="SideMember.Count"/> copies of its character, or that many rolls of its
    /// template; the reason when a record is missing.
    /// </summary>
    string? Expand(SideMember line, ulong seed, ulong key, List<SideMember> into)
    {
        if (line.Count < 1) return "a member's count must be at least 1";
        if (line.Template is { } template) return ExpandTemplate(line, template, seed, key, into);
        if (db.Find(line.Character ?? "") is not { Type: FcsRecordType.Character or FcsRecordType.AnimalCharacter })
            return $"no character record '{line.Character}'";
        if (line.Race is { } race && db.Find(race) is not { Type: FcsRecordType.Race }) return $"no race record '{race}'";

        for (int n = 0; n < line.Count; n++) into.Add(line with { Count = 1 });
        return null;
    }

    /// <summary>
    /// Rolls a template's roster <see cref="SideMember.Count"/> times as the game does at squad size 1, keeping its fighters (not its slaves or
    /// prisoners); the line's race, gear and stats apply to each.
    /// </summary>
    string? ExpandTemplate(SideMember line, string templateId, ulong seed, ulong key, List<SideMember> into)
    {
        if (db.Find(templateId) is not { } record || !SquadFactory.IsTemplate(record)) return $"no squad template '{templateId}'";

        var template = SquadTemplate.From(record);
        for (int n = 0; n < line.Count; n++)
        {
            var plan = SquadFactory.Plan(db, template, 1, seed, key + (ulong)n, checkWorldState: false);
            foreach (var member in plan.Members)
                into.Add(line with { Template = null, Character = member.RecordId, Count = 1 });
        }

        return null;
    }

    #endregion

    #region Kit

    /// <summary>
    /// Queues a fighter's loadout (its own gear destroyed first) and stats for the next tick.
    /// </summary>
    static void Outfit(World world, CharacterId id, SideMember member)
    {
        long next = world.Tick + 1;
        if (member.Gear is { } gear)
        {
            world.Defer(new DebugStripItems(id, BackpackOnly: false) { Tick = next });
            foreach (var item in gear) world.Defer(Give(id, item, next));
        }

        foreach (var (stat, value) in member.Stats ?? [])
            world.Defer(new DebugSetStats([id], [ArenaStats.Named(stat)], value) { Tick = next });
    }

    /// <summary>
    /// The command that gives and wears one item: an exact weapon grade when the item names one, else its quality.
    /// </summary>
    static DebugGiveItem Give(CharacterId id, GearItem item, long tick)
    {
        bool exact = item.Maker is not null || item.Model is not null || item.Level is not null;
        return new DebugGiveItem(id, item.Item, 1, item.Quality ?? 0)
        {
            Tick = tick,
            Equip = true,
            Exact = exact ? new DebugWeaponGrade(item.Maker, item.Model, item.Level) : null,
        };
    }

    #endregion
}
