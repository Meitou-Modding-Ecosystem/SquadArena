using Meitou.Data.Gameplay.Bodies;

namespace SquadArena;

/// <summary>
/// The stat names a squad file uses (docs/squad-rating.md "Stats"): the names of <see cref="StatsEnumerated"/>, any case, spaces and underscores
/// ignored.
/// </summary>
public static class ArenaStats
{
    /// <summary>
    /// The stat a squad file names: an enum name of <see cref="StatsEnumerated"/>, any case, spaces and underscores ignored ("melee attack").
    /// Throws for an unknown name (SquadRating checks names before writing a request).
    /// </summary>
    public static StatsEnumerated Named(string name) =>
        TryParse(name, out var stat) ? stat : throw new ArgumentException($"no stat named '{name}'", nameof(name));

    /// <summary>
    /// The stat a squad file names; false for an unknown name or one with no storage of its own.
    /// </summary>
    public static bool TryParse(string name, out StatsEnumerated stat)
    {
        string wanted = Squash(name);
        foreach (var candidate in Enum.GetValues<StatsEnumerated>())
        {
            if (Squash(candidate.ToString()) != wanted || StatsInfo.StorageOf(candidate) == StatsEnumerated.None) continue;

            stat = candidate;
            return true;
        }

        stat = StatsEnumerated.None;
        return false;
    }

    /// <summary>
    /// A name in lower case with spaces, underscores and dashes taken out.
    /// </summary>
    static string Squash(string name) => new([.. name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
