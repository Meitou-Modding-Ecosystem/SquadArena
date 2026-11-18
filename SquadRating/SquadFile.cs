using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SquadArena;

namespace SquadRating;

/// <summary>
/// A squad file (docs/squad-rating.md "Squad files"): a name and the members, each a character (with optional race, gear and stats) or a squad
/// template, every record given by its in-game name or its id.
/// </summary>
sealed record SquadFile
{
    /// <summary>
    /// The squad's name, as results show it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The members, in spawn order.
    /// </summary>
    public required SideMember[] Members { get; init; }

    /// <summary>
    /// Reads a squad file.
    /// </summary>
    public static SquadFile Load(string path)
    {
        var squad = JsonSerializer.Deserialize<SquadFile>(File.ReadAllText(path), FightRequest.Json)
            ?? throw new InvalidDataException($"{path} is empty");
        if (squad.Members is not { Length: > 0 }) throw new InvalidDataException($"{path} has no members");
        return squad;
    }

    /// <summary>
    /// Writes the file.
    /// </summary>
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, FightRequest.Json) + "\n");

    /// <summary>
    /// The squad as an arena side: every name replaced by its id; the errors when a name does not resolve or a stat is unknown.
    /// </summary>
    public (SideRequest? Side, IReadOnlyList<string> Errors) Resolve(GameLookup game)
    {
        var errors = new List<string>();
        var members = Members.Select((m, i) => ResolveMember(m, i + 1, game, errors)).ToArray();
        return errors.Count > 0 ? (null, errors) : (new SideRequest { Name = Name, Members = members }, errors);
    }

    /// <summary>
    /// The key fights of this squad are filed under: its name and a hash of its resolved members, so an edited squad starts a new record.
    /// </summary>
    public static string KeyOf(SideRequest side)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(side.Members, FightRequest.Json)));
        return $"squad:{side.Name}#{Convert.ToHexStringLower(hash)[..10]}";
    }

    /// <summary>
    /// One member with its records resolved.
    /// </summary>
    static SideMember ResolveMember(SideMember m, int line, GameLookup game, List<string> errors)
    {
        string? Id(string? value, string kind)
        {
            if (value is null) return null;
            var (id, error) = game.Resolve(value, kind);
            if (error is not null) errors.Add($"member {line}: {error}");
            return id;
        }

        if ((m.Character is null) == (m.Template is null)) errors.Add($"member {line}: give either \"character\" or \"template\"");
        if (m.Count < 1) errors.Add($"member {line}: \"count\" must be at least 1");
        foreach (string stat in m.Stats?.Keys ?? Enumerable.Empty<string>())
            if (!ArenaStats.TryParse(stat, out _)) errors.Add($"member {line}: no stat named '{stat}' (see docs/squad-rating.md \"Stats\")");

        return m with
        {
            Character = Id(m.Character, "character"),
            Template = Id(m.Template, "template"),
            Race = Id(m.Race, "race"),
            Gear = m.Gear?.Select(ResolveGear).ToArray(),
        };

        GearItem ResolveGear(GearItem g) => g with { Item = Id(g.Item, "item") ?? g.Item, Maker = Id(g.Maker, "maker"), Model = Id(g.Model, "model") };
    }
}
