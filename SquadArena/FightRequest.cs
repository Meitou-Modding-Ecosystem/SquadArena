using System.Text.Json;
using System.Text.Json.Serialization;

namespace SquadArena;

/// <summary>
/// One fight the arena is asked for (docs/squad-rating.md "Request and result"): two sides spawned 40 units apart at <see cref="At"/>, fought until
/// one side has nobody standing or <see cref="TimeoutSeconds"/> of game time pass. Every record is a string id: SquadRating resolves names first.
/// </summary>
public sealed record FightRequest
{
    /// <summary>
    /// The fight's id: the request's file name without <c>.json</c>, and the result's.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The world point (x, z) between the two sides.
    /// </summary>
    public required float[] At { get; init; }

    /// <summary>
    /// Game seconds after which an undecided fight ends as a draw.
    /// </summary>
    public float TimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Rolls a template member's roster (<see cref="SideMember.Template"/>) and the fighters' looks.
    /// </summary>
    public ulong Seed { get; init; }

    /// <summary>
    /// The two sides, side 0 south of <see cref="At"/>, side 1 north.
    /// </summary>
    public required SideRequest[] Sides { get; init; }

    /// <summary>
    /// The JSON options of every arena file: camel case, nulls left out, indented.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };
}

/// <summary>
/// One side of a fight: a label and its members.
/// </summary>
public sealed record SideRequest
{
    /// <summary>
    /// The label results and logs use (the squad's name, or "6 x Dust Bandit").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The members, in spawn order.
    /// </summary>
    public required SideMember[] Members { get; init; }
}

/// <summary>
/// A line of a side: <see cref="Count"/> copies of a CHARACTER (or ANIMAL_CHARACTER) with optional race, gear and stats, or one roll of a squad
/// template's roster at squad size 1.
/// </summary>
public sealed record SideMember
{
    /// <summary>
    /// A note for people reading the file (capture writes the character's name); the arena ignores it.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The character record; null when <see cref="Template"/> is set.
    /// </summary>
    public string? Character { get; init; }

    /// <summary>
    /// A SQUAD_TEMPLATE whose roster is rolled as the game rolls it at squad size 1 (its slaves and prisoners left out).
    /// </summary>
    public string? Template { get; init; }

    /// <summary>
    /// How many copies of <see cref="Character"/>, or rolls of <see cref="Template"/>.
    /// </summary>
    public int Count { get; init; } = 1;

    /// <summary>
    /// The RACE to draw and build the body as; null keeps the record's.
    /// </summary>
    public string? Race { get; init; }

    /// <summary>
    /// Items to wear and wield. When given, the record's own gear is destroyed first, so the list is the whole loadout.
    /// </summary>
    public GearItem[]? Gear { get; init; }

    /// <summary>
    /// Stat values by <see cref="Meitou.Data.Gameplay.Bodies.StatsEnumerated"/> name, set after the spawn (the number the stats window shows).
    /// </summary>
    public Dictionary<string, float>? Stats { get; init; }
}

/// <summary>
/// One item of a loadout: a weapon's exact maker, model and level, or an armour piece's quality.
/// </summary>
public sealed record GearItem
{
    /// <summary>
    /// The item record (WEAPON, ARMOUR, CROSSBOW, ITEM).
    /// </summary>
    public required string Item { get; init; }

    /// <summary>
    /// A weapon's manufacturer (WEAPON_MANUFACTURER).
    /// </summary>
    public string? Maker { get; init; }

    /// <summary>
    /// A weapon's model (MATERIAL_SPECS_WEAPON).
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// A weapon's level.
    /// </summary>
    public int? Level { get; init; }

    /// <summary>
    /// Quality 0 to 100 for armour, crossbows and a weapon with no exact grade.
    /// </summary>
    public int? Quality { get; init; }
}
