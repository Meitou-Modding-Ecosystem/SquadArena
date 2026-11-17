namespace SquadArena;

/// <summary>
/// The outcome of a <see cref="FightRequest"/>, written as <c>results/&lt;id&gt;.json</c> (docs/squad-rating.md "Request and result").
/// </summary>
public sealed record FightResult
{
    /// <summary>
    /// The request's id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The side left standing (0 or 1); -1 for a draw (time ran out with both standing, or both went down on one tick).
    /// </summary>
    public int Winner { get; init; } = -1;

    /// <summary>
    /// "decided", "timeout", or "refused" (the request could not be fought; <see cref="Error"/> says why).
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Why a request was refused.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Game seconds from the first order to the end.
    /// </summary>
    public float Seconds { get; init; }

    /// <summary>
    /// Each side's fighters at the end.
    /// </summary>
    public SideOutcome[] Sides { get; init; } = [];

    /// <summary>
    /// Every fighter, by the index the log uses.
    /// </summary>
    public FighterInfo[] Fighters { get; init; } = [];

    /// <summary>
    /// What happened, in order: every landed hit, downing, recovery, death and lost limb.
    /// </summary>
    public FightEvent[] Log { get; init; } = [];
}

/// <summary>
/// A side's count at the end of a fight.
/// </summary>
/// <param name="Name">The side's label.</param>
/// <param name="Fighters">How many fighters it started with.</param>
/// <param name="Standing">Not incapacitated.</param>
/// <param name="Alive">Not dead (knocked out counts).</param>
/// <param name="DamageDealt">Cut, blunt and pierce its fighters delivered after armour, summed.</param>
public sealed record SideOutcome(string Name, int Fighters, int Standing, int Alive, float DamageDealt);

/// <summary>
/// A fighter as spawned.
/// </summary>
/// <param name="Index">Its number in the log.</param>
/// <param name="Side">0 or 1.</param>
/// <param name="Name">The name the game gave it.</param>
/// <param name="Character">Its CHARACTER record.</param>
/// <param name="Race">Its RACE record.</param>
/// <param name="Weapon">The weapon it carries at the start (its name), or null when unarmed.</param>
public sealed record FighterInfo(int Index, int Side, string Name, string Character, string? Race, string? Weapon);

/// <summary>
/// One line of the fight log. <see cref="Kind"/> is <c>hit</c>, <c>down</c> (knocked out), <c>up</c> (back on its feet), <c>dead</c>,
/// <c>severed</c> or <c>crushed</c> (a limb).
/// </summary>
public sealed record FightEvent
{
    /// <summary>
    /// Game seconds since the first order.
    /// </summary>
    public float T { get; init; }

    /// <summary>
    /// What happened.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The fighter it happened to (a hit's victim).
    /// </summary>
    public int Fighter { get; init; }

    /// <summary>
    /// A hit's attacker; null for other kinds, and for a hit from someone outside the fight (-1).
    /// </summary>
    public int? By { get; init; }

    /// <summary>
    /// The attacker's weapon (its name), "unarmed", or the crossbow for a bolt.
    /// </summary>
    public string? Weapon { get; init; }

    /// <summary>
    /// "melee" or "projectile".
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// The body part hit, or the limb lost.
    /// </summary>
    public string? Part { get; init; }

    /// <summary>
    /// Damage delivered after armour: cut, blunt, pierce.
    /// </summary>
    public float[]? Damage { get; init; }

    /// <summary>
    /// Damage the armour absorbed: cut, blunt, pierce.
    /// </summary>
    public float[]? Absorbed { get; init; }

    /// <summary>
    /// The victim was already down before this hit.
    /// </summary>
    public bool? OnDowned { get; init; }
}
