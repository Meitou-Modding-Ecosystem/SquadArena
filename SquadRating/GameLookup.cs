using Meitou.Data;
using Meitou.Data.Fcs;

namespace SquadRating;

/// <summary>
/// Finds records by name or id in the merged game data (docs/squad-rating.md "Names and ids"): the install's core files and the mods
/// <c>data/mods.cfg</c> lists, as the game loads them, so a mod's own items and characters resolve too.
/// </summary>
sealed class GameLookup(GameDatabase db)
{
    /// <summary>
    /// What a squad file field can name, and the record types each accepts.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, FcsRecordType[]> Kinds = new Dictionary<string, FcsRecordType[]>
    {
        ["character"] = [FcsRecordType.Character, FcsRecordType.AnimalCharacter],
        ["template"] = [FcsRecordType.SquadTemplate, FcsRecordType.UniqueSquadTemplate],
        ["race"] = [FcsRecordType.Race],
        ["item"] = [FcsRecordType.Weapon, FcsRecordType.Armour, FcsRecordType.Crossbow, FcsRecordType.Item],
        ["maker"] = [FcsRecordType.WeaponManufacturer],
        ["model"] = [FcsRecordType.MaterialSpecsWeapon],
    };

    /// <summary>
    /// The merged data.
    /// </summary>
    public GameDatabase Db { get; } = db;

    /// <summary>
    /// Records of the kinds given whose name or id contains <paramref name="text"/> (any case), exact names first, then by name.
    /// </summary>
    public IReadOnlyList<GameRecord> Find(string text, IEnumerable<string> kinds)
    {
        var types = kinds.SelectMany(k => Kinds[k]).ToHashSet();
        return [.. Db.Records.Values
            .Where(r => types.Contains(r.Type) && !string.IsNullOrEmpty(r.Name))
            .Where(r => r.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || r.StringId.Equals(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Name.Equals(text, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.StringId, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The id a field's value stands for: an id of the right kind as it is, else the one record of that kind with exactly that name (any case).
    /// The error lists the candidates when there are none or several.
    /// </summary>
    public (string? Id, string? Error) Resolve(string value, string kind)
    {
        var types = Kinds[kind];
        if (Db.Find(value) is { } byId && types.Contains(byId.Type)) return (byId.StringId, null);

        var named = Db.Records.Values
            .Where(r => types.Contains(r.Type) && r.Name.Equals(value, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.StringId, StringComparer.Ordinal)
            .ToList();
        if (named.Count == 1) return (named[0].StringId, null);
        if (named.Count > 1) return (null, $"{kind} '{value}' is ambiguous; use one of these ids:\n{Lines(named)}");

        var near = Find(value, [kind]).Take(8).ToList();
        string hint = near.Count > 0 ? $"; did you mean:\n{Lines(near)}" : $"; try: SquadRating find \"{value}\" --kind {kind}";
        return (null, $"no {kind} named '{value}'{hint}");
    }

    /// <summary>
    /// A record as one line: id, type, name and the file that defines it.
    /// </summary>
    public static string Line(GameRecord r) => $"{r.StringId,-28} {r.Type,-22} {r.Name}  ({r.DefinedBy})";

    /// <summary>
    /// Records as indented lines.
    /// </summary>
    static string Lines(IEnumerable<GameRecord> records) => string.Join('\n', records.Select(r => "  " + Line(r)));
}
