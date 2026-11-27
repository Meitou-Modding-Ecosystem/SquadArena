namespace SquadRating.Tests;

/// <summary>
/// Fights drawn from a known Bradley–Terry model, so a fit can be checked against the strengths that made them.
/// </summary>
static class MadeUpFights
{
    /// <summary>
    /// β the fights are drawn with.
    /// </summary>
    public const double Beta = 1.5;

    /// <summary>
    /// <paramref name="count"/> fights of a squad of strength <paramref name="strength"/> against 1 to 8 yardstick characters, sides alternating.
    /// </summary>
    public static IEnumerable<FightRecord> AgainstYardstick(SideKey squad, double strength, int count, Random random)
    {
        for (int k = 0; k < count; k++)
        {
            int n = 1 + k % 8;
            var yardstick = new SideKey(SideKey.YardstickPrefix + n, $"{n} x Dust Bandit");
            bool won = random.NextDouble() < StrengthFit.WinChance(strength, Beta * Math.Log(n));
            yield return k % 2 == 0
                ? new FightRecord($"f{k}", [squad, yardstick], won ? 0 : 1, "decided", 60)
                : new FightRecord($"f{k}", [yardstick, squad], won ? 1 : 0, "decided", 60);
        }
    }

    /// <summary>
    /// The strength worth <paramref name="rating"/> yardstick characters.
    /// </summary>
    public static double StrengthOf(double rating) => Beta * Math.Log(rating);
}
