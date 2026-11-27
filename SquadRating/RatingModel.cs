namespace SquadRating;

/// <summary>
/// One squad's fitted strength.
/// </summary>
/// <param name="Key">The squad's key.</param>
/// <param name="Name">Its label.</param>
/// <param name="Rating">How many yardstick characters it is worth: the count it beats half the time.</param>
/// <param name="Low">The low end of the 90% interval.</param>
/// <param name="High">The high end of the 90% interval.</param>
/// <param name="Fights">Fights it was in.</param>
/// <param name="Score">Its wins plus half its draws.</param>
sealed record RatedSquad(string Key, string Name, double Rating, double Low, double High, int Fights, double Score);

/// <summary>
/// The ranking fit (docs/squad-rating.md "The yardstick"): every squad on the fight list is its own <see cref="StrengthFit"/> parameter, and its
/// rating is exp(s / β): the yardstick count it beats half the time.
/// </summary>
static class RatingModel
{
    /// <summary>
    /// Fits every squad on the fight list; squads by rating, strongest first, and β.
    /// </summary>
    public static (IReadOnlyList<RatedSquad> Ratings, double Beta) Fit(IReadOnlyList<FightRecord> fights)
    {
        var squads = Squads(fights);
        var (x, covariance) = StrengthFit.Fit(fights, squads.Count, key => [squads[key].Index]);
        return (Ratings(fights, squads, x, covariance), x[^1]);
    }

    /// <summary>
    /// The squads (not yardsticks) on the list, each with its parameter index, and its label.
    /// </summary>
    static Dictionary<string, (int Index, string Name)> Squads(IReadOnlyList<FightRecord> fights)
    {
        var squads = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
        foreach (var side in fights.SelectMany(f => f.Sides))
            if (side.YardstickCount is null && !squads.ContainsKey(side.Key)) squads[side.Key] = (squads.Count, side.Name);

        return squads;
    }

    /// <summary>
    /// The ratings with their intervals (the strength's standard error from the inverse curvature, β taken as known).
    /// </summary>
    static List<RatedSquad> Ratings(IReadOnlyList<FightRecord> fights, Dictionary<string, (int Index, string Name)> squads, double[] x,
        double[,] covariance)
    {
        double beta = x[^1], z = StrengthFit.Z90;
        var list = new List<RatedSquad>();
        foreach (var (key, (index, name)) in squads)
        {
            double s = x[index], se = Math.Sqrt(Math.Max(covariance[index, index], 0));
            var mine = fights.Where(f => f.Sides.Any(side => side.Key == key)).ToList();
            double score = mine.Sum(f => f.Score(f.Sides[0].Key == key ? 0 : 1));
            list.Add(new RatedSquad(key, name, Math.Exp(s / beta), Math.Exp((s - z * se) / beta), Math.Exp((s + z * se) / beta), mine.Count, score));
        }

        return [.. list.OrderByDescending(r => r.Rating)];
    }
}
