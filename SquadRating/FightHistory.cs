using System.Text.Json;
using SquadArena;

namespace SquadRating;

/// <summary>
/// A side as the fight list files it: the key fights of the same squad share, and its label.
/// </summary>
/// <param name="Key"><c>squad:&lt;name&gt;#&lt;hash&gt;</c>, <c>template:&lt;id&gt;</c> or <c>yardstick:&lt;count&gt;</c>.</param>
/// <param name="Name">The label.</param>
sealed record SideKey(string Key, string Name)
{
    /// <summary>
    /// The prefix of a yardstick side's key; the count follows.
    /// </summary>
    public const string YardstickPrefix = "yardstick:";

    /// <summary>
    /// The yardstick count of this side, or null for a squad.
    /// </summary>
    public int? YardstickCount => Key.StartsWith(YardstickPrefix, StringComparison.Ordinal) ? int.Parse(Key[YardstickPrefix.Length..]) : null;
}

/// <summary>
/// One fought fight as the fight list keeps it.
/// </summary>
/// <param name="Id">The fight's id; its full result with the log is <c>&lt;runs&gt;/results/&lt;id&gt;.json</c>.</param>
/// <param name="Sides">Side 0 and side 1.</param>
/// <param name="Winner">0, 1, or -1 for a draw.</param>
/// <param name="Outcome">"decided" or "timeout".</param>
/// <param name="Seconds">Game seconds the fight took.</param>
sealed record FightRecord(string Id, SideKey[] Sides, int Winner, string Outcome, float Seconds)
{
    /// <summary>
    /// The score of side <paramref name="side"/>: 1 for a win, 0.5 for a draw, 0 for a loss.
    /// </summary>
    public double Score(int side) => Winner < 0 ? 0.5 : Winner == side ? 1 : 0;
}

/// <summary>
/// The fight list (<c>&lt;runs&gt;/fights.jsonl</c>): one line per fight, every fight ever run from this folder; the ratings are fitted from it.
/// </summary>
sealed class FightHistory(string runsDir)
{
    /// <summary>
    /// The list's path.
    /// </summary>
    string ListPath => Path.Combine(runsDir, "fights.jsonl");

    /// <summary>
    /// The folder full results are copied into.
    /// </summary>
    public string ResultsDir => Path.Combine(runsDir, "results");

    /// <summary>
    /// Every fight on the list, oldest first.
    /// </summary>
    public List<FightRecord> All() =>
        File.Exists(ListPath)
            ? [.. File.ReadLines(ListPath)
                .Where(l => l.Length > 0)
                .Select(l => JsonSerializer.Deserialize<FightRecord>(l, FightRequest.Json)!)]
            : [];

    /// <summary>
    /// Adds a fight, with its full result.
    /// </summary>
    public void Add(FightRecord record, FightResult result)
    {
        Directory.CreateDirectory(ResultsDir);
        File.WriteAllText(Path.Combine(ResultsDir, record.Id + ".json"), JsonSerializer.Serialize(result, FightRequest.Json));
        var compact = new JsonSerializerOptions(FightRequest.Json) { WriteIndented = false };
        File.AppendAllText(ListPath, JsonSerializer.Serialize(record, compact) + "\n");
    }
}
