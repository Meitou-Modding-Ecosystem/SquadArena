using SquadArena;

namespace SquadRating;

/// <summary>
/// Measures a squad against the yardstick (docs/squad-rating.md "The yardstick"): an up-and-down staircase over the count of yardstick characters,
/// up after a win and down after a loss, in steps that shrink from ×1.6 to ×1.15, so the fights gather round the count the squad beats half the
/// time, where each fight says the most. The squad takes side 0 and side 1 in turn. The fit (<see cref="RatingModel"/>) then uses every fight.
/// </summary>
sealed class Ladder(FightRunner runner, string yardstickId, string yardstickName)
{
    /// <summary>
    /// The most yardstick characters in one fight.
    /// </summary>
    public const int MaxCount = 60;

    /// <summary>
    /// The first and last staircase step.
    /// </summary>
    const double FirstStep = 1.6, LastStep = 1.15;

    /// <summary>
    /// Runs <paramref name="fights"/> fights of the squad against the yardstick, starting at <paramref name="start"/> characters.
    /// </summary>
    public void Climb(Contender squad, int fights, double start)
    {
        double count = Math.Clamp(start, 1, MaxCount), step = FirstStep;
        for (int k = 0; k < fights; k++)
        {
            int n = (int)Math.Clamp(Math.Round(count), 1, MaxCount);
            var yardstick = Yardstick(n);
            bool first = k % 2 == 0;
            var record = first ? runner.Fight(squad, yardstick) : runner.Fight(yardstick, squad);
            double score = record.Score(first ? 0 : 1);
            if (score > 0.5) count *= step;
            else if (score < 0.5) count /= step;

            step = Math.Max(LastStep, step * 0.85);
        }
    }

    /// <summary>
    /// <paramref name="n"/> yardstick characters as a side.
    /// </summary>
    public Contender Yardstick(int n) => new(new SideKey(SideKey.YardstickPrefix + n, $"{n} x {yardstickName}"),
        new SideRequest { Name = $"{n} x {yardstickName}", Members = [new SideMember { Character = yardstickId, Count = n }] });

    /// <summary>
    /// Where a squad's staircase starts: its listed fighters (a template line counts 3).
    /// </summary>
    public static double StartOf(SideRequest side) => Math.Max(1, side.Members.Sum(m => m.Template is null ? m.Count : 3 * m.Count));
}
