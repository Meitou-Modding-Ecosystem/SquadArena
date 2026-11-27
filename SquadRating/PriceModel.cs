namespace SquadRating;

/// <summary>
/// One line of a price list.
/// </summary>
/// <param name="Group">The option's group; "baseline" for the baseline.</param>
/// <param name="Name">The option's name.</param>
/// <param name="Points">What it adds to the baseline's points (the baseline's line: its own points).</param>
/// <param name="Low">The low end of the 90% interval.</param>
/// <param name="High">The high end of the 90% interval.</param>
/// <param name="Factor">How many times the baseline's rating the baseline with this option is worth.</param>
/// <param name="Fights">Fights of fighters with the option (the baseline's line: of the bare baseline).</param>
sealed record PriceLine(string Group, string Name, double Points, double Low, double High, double Factor, int Fights);

/// <summary>
/// The price fit (docs/squad-rating.md "Price lists"): a <see cref="StrengthFit"/> in which a variant's strength is the baseline's plus the effect
/// of each option it has, fitted on every fight of the sheet's variants against the yardstick or each other. An option's price is what it adds to
/// the baseline's rating, in points: <c>points per yardstick × (exp((b + e) / β) − exp(b / β))</c>, its interval by the delta method (β taken as
/// known). Effects add in strength, so prices add exactly for the baseline plus one option; mixes show how far they add beyond that.
/// </summary>
static class PriceModel
{
    /// <summary>
    /// The price list: the baseline's line, then each option's, sheet order.
    /// </summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="variantKeys">Each variant's fight key with the options it has.</param>
    /// <param name="fights">Every fight in the folder; fights of other squads are left out.</param>
    public static IReadOnlyList<PriceLine> Fit(PriceSheet sheet, IReadOnlyDictionary<string, int[]> variantKeys, IReadOnlyList<FightRecord> fights)
    {
        var options = sheet.AllOptions();
        var (x, covariance) = StrengthFit.Fit(fights, options.Count + 1, key => Features(variantKeys, key));
        var counted = fights.Where(f => f.Sides.Any(s => variantKeys.ContainsKey(s.Key))).ToList();
        var lines = new List<PriceLine> { Baseline(sheet, x, covariance, Fights(counted, variantKeys, k => k.Length == 0)) };
        lines.AddRange(options.Select((o, i) => Option(sheet, o, i + 1, x, covariance, Fights(counted, variantKeys, k => k.Contains(i)))));
        return lines;
    }

    /// <summary>
    /// A variant's features: the baseline (0) and each of its options (1 on); null for a squad that is not a variant.
    /// </summary>
    static int[]? Features(IReadOnlyDictionary<string, int[]> variantKeys, string key) =>
        variantKeys.TryGetValue(key, out var options) ? [0, .. options.Select(o => o + 1)] : null;

    /// <summary>
    /// The baseline's line: its rating in points.
    /// </summary>
    static PriceLine Baseline(PriceSheet sheet, double[] x, double[,] covariance, int fights)
    {
        double beta = x[^1], se = Math.Sqrt(Math.Max(covariance[0, 0], 0)), unit = sheet.PointsPerYardstick, z = StrengthFit.Z90;
        double Points(double s) => unit * Math.Exp(s / beta);
        return new PriceLine("baseline", "baseline", Points(x[0]), Points(x[0] - z * se), Points(x[0] + z * se), 1, fights);
    }

    /// <summary>
    /// An option's line: what parameter <paramref name="p"/> adds to the baseline, in points.
    /// </summary>
    static PriceLine Option(PriceSheet sheet, PricedOption option, int p, double[] x, double[,] covariance, int fights)
    {
        double beta = x[^1], unit = sheet.PointsPerYardstick;
        double without = Math.Exp(x[0] / beta), with = Math.Exp((x[0] + x[p]) / beta);
        double points = unit * (with - without);

        // The delta method: d points / d b = unit (with − without) / β, d points / d e = unit · with / β.
        double gb = unit * (with - without) / beta, ge = unit * with / beta;
        double variance = gb * gb * covariance[0, 0] + 2 * gb * ge * covariance[0, p] + ge * ge * covariance[p, p];
        double half = StrengthFit.Z90 * Math.Sqrt(Math.Max(variance, 0));
        return new PriceLine(option.Group, option.Name, points, points - half, points + half, with / without, fights);
    }

    /// <summary>
    /// Fights with a variant whose options match <paramref name="has"/>.
    /// </summary>
    static int Fights(List<FightRecord> fights, IReadOnlyDictionary<string, int[]> variantKeys, Func<int[], bool> has) =>
        fights.Count(f => f.Sides.Any(s => variantKeys.TryGetValue(s.Key, out var o) && has(o)));
}
