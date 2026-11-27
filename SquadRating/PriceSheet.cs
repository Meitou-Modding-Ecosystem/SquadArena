using System.Text.Json;
using SquadArena;

namespace SquadRating;

/// <summary>
/// A price sheet (docs/squad-rating.md "Price lists"): one baseline fighter and the options a point-buy system would charge for (a race, a gear
/// piece in a slot, stat values). <see cref="Variants"/> makes the fighters to rate: the baseline, the baseline with each option, and optional mixes
/// of options from different groups that check the prices add up.
/// </summary>
sealed record PriceSheet
{
    /// <summary>
    /// The sheet's name; every variant's name starts with it, so its fights are filed apart from other sheets'.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Points one yardstick character costs; the price list's unit.
    /// </summary>
    public double PointsPerYardstick { get; init; } = 100;

    /// <summary>
    /// The fighter every option is priced on top of.
    /// </summary>
    public required PriceFighter Baseline { get; init; }

    /// <summary>
    /// The options, each changing the baseline in one way.
    /// </summary>
    public PriceOption[] Options { get; init; } = [];

    /// <summary>
    /// Stat levels to price: each sweep adds one option per level, setting all its stats to that level.
    /// </summary>
    public StatSweep[] StatSweeps { get; init; } = [];

    /// <summary>
    /// Seeds the choice of mixes, so the same sheet always makes the same mixes and their fights add up over runs.
    /// </summary>
    public int Seed { get; init; } = 1;

    /// <summary>
    /// Reads a price sheet.
    /// </summary>
    public static PriceSheet Load(string path)
    {
        var sheet = JsonSerializer.Deserialize<PriceSheet>(File.ReadAllText(path), FightRequest.Json)
            ?? throw new InvalidDataException($"{path} is empty");
        if (sheet.Baseline.Character is null) throw new InvalidDataException($"{path}: the baseline needs a \"character\"");
        return sheet;
    }

    /// <summary>
    /// Every option, the sweeps' levels after the listed ones, each with its group.
    /// </summary>
    public IReadOnlyList<PricedOption> AllOptions()
    {
        var listed = Options.Select(o => new PricedOption(o.Group ?? GroupOf(o), o.Name, o));
        var swept = StatSweeps.SelectMany(s => s.Levels.Select(level => new PricedOption(s.Name, $"{s.Name} {level:0.#}", s.At(level))));
        return [.. listed, .. swept];
    }

    /// <summary>
    /// The fighters to rate: the baseline, one per option and <paramref name="mixes"/> mixes of options from two or more groups.
    /// </summary>
    public IReadOnlyList<PriceVariant> Variants(int mixes)
    {
        var options = AllOptions();
        var variants = new List<PriceVariant> { Variant([], options) };
        variants.AddRange(options.Select((_, i) => Variant([i], options)));
        variants.AddRange(Mixes(options, mixes).Select(m => Variant(m, options)));
        return variants;
    }

    /// <summary>
    /// The group an option without one falls in: what it changes.
    /// </summary>
    static string GroupOf(PriceOption o)
    {
        if (o.Race is not null) return "race";
        if (o.Gear is { Count: > 0 } g) return "gear: " + string.Join(", ", g.Keys.Order(StringComparer.Ordinal));
        if (o.Stats is { Count: > 0 } s) return "stats: " + string.Join(", ", s.Keys.Order(StringComparer.OrdinalIgnoreCase));
        return "other";
    }

    /// <summary>
    /// Up to <paramref name="count"/> distinct mixes: two or more groups, one option from each.
    /// </summary>
    List<int[]> Mixes(IReadOnlyList<PricedOption> options, int count)
    {
        var groups = options.Select((o, i) => (o.Group, i)).GroupBy(p => p.Group).Select(g => g.Select(p => p.i).ToArray()).ToArray();
        var mixes = new List<int[]>();
        if (groups.Length < 2) return mixes;

        var random = new Random(Seed);
        var seen = new HashSet<string>();
        for (int tries = 0; mixes.Count < count && tries < count * 20; tries++)
        {
            var mix = Mix(groups, random);
            if (seen.Add(string.Join(',', mix))) mixes.Add(mix);
        }

        return mixes;
    }

    /// <summary>
    /// One random mix: a random two or more groups, a random option of each, in option order.
    /// </summary>
    static int[] Mix(int[][] groups, Random random)
    {
        int take = random.Next(2, groups.Length + 1);
        var chosen = groups.OrderBy(_ => random.Next()).Take(take);
        return [.. chosen.Select(g => g[random.Next(g.Length)]).Order()];
    }

    /// <summary>
    /// The baseline with the options <paramref name="picked"/> applied in order.
    /// </summary>
    PriceVariant Variant(int[] picked, IReadOnlyList<PricedOption> options)
    {
        var fighter = picked.Aggregate(Baseline, (f, i) => f.With(options[i].Change));
        string label = picked.Length == 0 ? "baseline" : string.Join(" + ", picked.Select(i => options[i].Name));
        var squad = new SquadFile { Name = $"{Name}: {label}", Members = [fighter.Member()] };
        return new PriceVariant(label, picked, squad);
    }
}

/// <summary>
/// A fighter as a price sheet gives it: gear by slot, so an option can swap one piece.
/// </summary>
sealed record PriceFighter
{
    /// <summary>
    /// The CHARACTER record: body and default stats.
    /// </summary>
    public string? Character { get; init; }

    /// <summary>
    /// The RACE; null keeps the character's.
    /// </summary>
    public string? Race { get; init; }

    /// <summary>
    /// The whole loadout by slot name (any names: "weapon", "body", "head"...); nothing listed fights bare-handed and unarmoured.
    /// </summary>
    public Dictionary<string, GearItem?> Gear { get; init; } = [];

    /// <summary>
    /// Stat values as the stats window shows them.
    /// </summary>
    public Dictionary<string, float> Stats { get; init; } = [];

    /// <summary>
    /// This fighter changed by an option: its race, its gear slots (null empties a slot) and its stats replace this one's.
    /// </summary>
    public PriceFighter With(PriceOption change)
    {
        var gear = new Dictionary<string, GearItem?>(Gear);
        foreach (var (slot, item) in change.Gear ?? []) gear[slot] = item;

        var stats = new Dictionary<string, float>(Stats, StringComparer.OrdinalIgnoreCase);
        foreach (var (stat, value) in change.Stats ?? []) stats[stat] = value;

        return this with { Race = change.Race ?? Race, Gear = gear, Stats = stats };
    }

    /// <summary>
    /// The fighter as a squad file member: the slots' items in slot order make the loadout.
    /// </summary>
    public SideMember Member() => new()
    {
        Character = Character,
        Race = Race,
        Gear = [.. Gear.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value).OfType<GearItem>()],
        Stats = Stats.Count > 0 ? new Dictionary<string, float>(Stats.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)) : null,
    };
}

/// <summary>
/// One change to the baseline a point-buy would charge for.
/// </summary>
sealed record PriceOption
{
    /// <summary>
    /// Its name in the price list.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Options in one group are alternatives (a mix takes one at most); null groups by what the option changes.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// The race to build the body as.
    /// </summary>
    public string? Race { get; init; }

    /// <summary>
    /// Gear by slot; null empties the slot.
    /// </summary>
    public Dictionary<string, GearItem?>? Gear { get; init; }

    /// <summary>
    /// Stat values.
    /// </summary>
    public Dictionary<string, float>? Stats { get; init; }
}

/// <summary>
/// A stat sweep: one option per level, each setting every listed stat to it.
/// </summary>
sealed record StatSweep
{
    /// <summary>
    /// The group's name, and the start of each level's.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The stats set together.
    /// </summary>
    public required string[] Stats { get; init; }

    /// <summary>
    /// The levels to price.
    /// </summary>
    public required float[] Levels { get; init; }

    /// <summary>
    /// The option for one level.
    /// </summary>
    public PriceOption At(float level) => new()
    {
        Name = $"{Name} {level:0.#}",
        Group = Name,
        Stats = Stats.ToDictionary(s => s, _ => level, StringComparer.OrdinalIgnoreCase),
    };
}

/// <summary>
/// An option with its group settled.
/// </summary>
/// <param name="Group">Its group.</param>
/// <param name="Name">Its name.</param>
/// <param name="Change">What it changes.</param>
sealed record PricedOption(string Group, string Name, PriceOption Change);

/// <summary>
/// One fighter to rate: its label, the options it has (indices into <see cref="PriceSheet.AllOptions"/>) and its squad file.
/// </summary>
/// <param name="Label">"baseline", or the options' names joined with " + ".</param>
/// <param name="Options">The options applied.</param>
/// <param name="Squad">The fighter as a one-member squad file.</param>
sealed record PriceVariant(string Label, int[] Options, SquadFile Squad);
