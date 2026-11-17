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
/// The ranking fit (docs/squad-rating.md "The yardstick"): a Bradley–Terry model, where side A beats side B with chance σ(s_A − s_B), a squad's
/// strength s is its own parameter and n yardstick characters have s = β·ln n (one yardstick character is 0). Fitted by Newton's method on the
/// log-likelihood of every fight (a draw counts half a win each), with a weak normal prior on each squad's s and on β so a squad that won or lost
/// every fight still gets a finite number. A squad's rating is exp(s / β): the yardstick count it beats half the time.
/// </summary>
static class RatingModel
{
    /// <summary>
    /// The prior's weight on each squad strength (standard deviation about 7).
    /// </summary>
    const double StrengthPrior = 0.02;

    /// <summary>
    /// β's prior centre and weight (standard deviation 1).
    /// </summary>
    const double BetaCentre = 1.5, BetaPrior = 1;

    /// <summary>
    /// Newton steps at most.
    /// </summary>
    const int MaxSteps = 100;

    /// <summary>
    /// z for a two-sided 90% interval.
    /// </summary>
    const double Z90 = 1.645;

    /// <summary>
    /// Fits every squad on the fight list; squads by rating, strongest first, and β.
    /// </summary>
    public static (IReadOnlyList<RatedSquad> Ratings, double Beta) Fit(IReadOnlyList<FightRecord> fights)
    {
        var squads = Squads(fights);
        int n = squads.Count + 1;
        var x = new double[n];
        x[^1] = BetaCentre;
        double[,] hessian = new double[n, n];
        for (int step = 0; step < MaxSteps; step++)
        {
            var (gradient, h) = Derivatives(fights, squads, x);
            hessian = h;
            var delta = Solve(Negate(h), gradient);
            for (int i = 0; i < n; i++) x[i] += delta[i];

            x[^1] = Math.Max(x[^1], 0.05);
            if (delta.Max(Math.Abs) < 1e-9) break;
        }

        return (Ratings(fights, squads, x, Invert(Negate(hessian))), x[^1]);
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
    /// The gradient and Hessian of the log-posterior at <paramref name="x"/> (squad strengths, then β).
    /// </summary>
    static (double[] Gradient, double[,] Hessian) Derivatives(IReadOnlyList<FightRecord> fights, Dictionary<string, (int Index, string)> squads,
        double[] x)
    {
        int n = x.Length;
        var g = new double[n];
        var h = new double[n, n];
        foreach (var fight in fights)
        {
            var d = new double[n];
            Gradient(fight.Sides[0], +1, squads, d);
            Gradient(fight.Sides[1], -1, squads, d);
            double margin = Dot(d, x), p = Sigmoid(margin), residual = fight.Score(0) - p, weight = p * (1 - p);
            for (int i = 0; i < n; i++)
            {
                g[i] += residual * d[i];
                for (int j = 0; j < n; j++) h[i, j] -= weight * d[i] * d[j];
            }
        }

        AddPrior(x, g, h);
        return (g, h);
    }

    /// <summary>
    /// Adds a side's share of the margin's gradient: its squad's index, or ln count on β for a yardstick.
    /// </summary>
    static void Gradient(SideKey side, int sign, Dictionary<string, (int Index, string)> squads, double[] d)
    {
        if (side.YardstickCount is { } count) d[^1] += sign * Math.Log(count);
        else d[squads[side.Key].Index] += sign;
    }

    /// <summary>
    /// The priors' gradient and curvature.
    /// </summary>
    static void AddPrior(double[] x, double[] g, double[,] h)
    {
        int n = x.Length;
        for (int i = 0; i < n - 1; i++)
        {
            g[i] -= StrengthPrior * x[i];
            h[i, i] -= StrengthPrior;
        }

        g[^1] -= BetaPrior * (x[^1] - BetaCentre);
        h[n - 1, n - 1] -= BetaPrior;
    }

    /// <summary>
    /// The ratings with their intervals (the strength's standard error from the inverse curvature, β taken as known).
    /// </summary>
    static List<RatedSquad> Ratings(IReadOnlyList<FightRecord> fights, Dictionary<string, (int Index, string Name)> squads, double[] x,
        double[,] covariance)
    {
        double beta = x[^1];
        var list = new List<RatedSquad>();
        foreach (var (key, (index, name)) in squads)
        {
            double s = x[index], se = Math.Sqrt(Math.Max(covariance[index, index], 0));
            var mine = fights.Where(f => f.Sides.Any(side => side.Key == key)).ToList();
            double score = mine.Sum(f => f.Score(f.Sides[0].Key == key ? 0 : 1));
            list.Add(new RatedSquad(key, name, Math.Exp(s / beta), Math.Exp((s - Z90 * se) / beta), Math.Exp((s + Z90 * se) / beta), mine.Count, score));
        }

        return [.. list.OrderByDescending(r => r.Rating)];
    }

    #region Linear algebra

    /// <summary>
    /// The logistic function.
    /// </summary>
    static double Sigmoid(double v) => 1 / (1 + Math.Exp(-v));

    /// <summary>
    /// The dot product.
    /// </summary>
    static double Dot(double[] a, double[] b) => a.Select((v, i) => v * b[i]).Sum();

    /// <summary>
    /// The matrix times -1.
    /// </summary>
    static double[,] Negate(double[,] m)
    {
        var r = (double[,])m.Clone();
        for (int i = 0; i < r.GetLength(0); i++)
            for (int j = 0; j < r.GetLength(1); j++) r[i, j] = -r[i, j];

        return r;
    }

    /// <summary>
    /// Solves a·v = b by Gaussian elimination with partial pivoting (a is positive definite here).
    /// </summary>
    static double[] Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = new double[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) m[i, j] = a[i, j];
            m[i, n] = b[i];
        }

        Eliminate(m, n);
        return [.. Enumerable.Range(0, n).Select(i => m[i, n])];
    }

    /// <summary>
    /// The inverse of a (positive definite) matrix.
    /// </summary>
    static double[,] Invert(double[,] a)
    {
        int n = a.GetLength(0);
        var m = new double[n, 2 * n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) m[i, j] = a[i, j];
            m[i, n + i] = 1;
        }

        Eliminate(m, n);
        var inverse = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++) inverse[i, j] = m[i, n + j];

        return inverse;
    }

    /// <summary>
    /// Gauss–Jordan elimination of the first <paramref name="n"/> columns of an augmented matrix to the identity.
    /// </summary>
    static void Eliminate(double[,] m, int n)
    {
        int width = m.GetLength(1);
        for (int col = 0; col < n; col++)
        {
            int pivot = Enumerable.Range(col, n - col).MaxBy(r => Math.Abs(m[r, col]));
            for (int j = 0; j < width; j++) (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);

            double lead = m[col, col];
            for (int j = 0; j < width; j++) m[col, j] /= lead;
            for (int r = 0; r < n; r++)
            {
                if (r == col || m[r, col] == 0) continue;

                double factor = m[r, col];
                for (int j = 0; j < width; j++) m[r, j] -= factor * m[col, j];
            }
        }
    }

    #endregion
}
