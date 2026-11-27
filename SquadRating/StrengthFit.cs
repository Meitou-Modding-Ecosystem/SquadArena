namespace SquadRating;

/// <summary>
/// The Bradley–Terry fit both the ratings (<see cref="RatingModel"/>) and the price lists (<see cref="PriceModel"/>) rest on: side A beats side B
/// with chance σ(s_A − s_B). A squad's strength s is the sum of the parameters its features name; n yardstick characters have s = β·ln n. Fitted by
/// Newton's method on the log-likelihood of every fight (a draw counts half a win each), with a weak normal prior on each parameter and on β, so
/// a squad that won or lost every fight still gets a finite number.
/// </summary>
static class StrengthFit
{
    /// <summary>
    /// The prior's weight on each parameter (standard deviation about 7).
    /// </summary>
    const double ParameterPrior = 0.02;

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
    public const double Z90 = 1.645;

    /// <summary>
    /// Fits <paramref name="parameters"/> parameters and β to the fights whose every squad side has features (<paramref name="featuresOf"/>
    /// returns null for a squad outside the fit; yardstick sides always count). The fitted values (β last) and their covariance.
    /// </summary>
    public static (double[] Values, double[,] Covariance) Fit(IReadOnlyList<FightRecord> fights, int parameters,
        Func<string, IReadOnlyList<int>?> featuresOf)
    {
        var rows = Rows(fights, parameters + 1, featuresOf);
        var x = new double[parameters + 1];
        x[^1] = BetaCentre;
        for (int step = 0; step < MaxSteps; step++)
        {
            var (gradient, h) = Derivatives(rows, x);
            var delta = Solve(Negate(h), gradient);
            x = Advance(rows, x, delta);
            if (delta.Max(Math.Abs) < 1e-9) break;
        }

        return (x, Invert(Negate(Derivatives(rows, x).Hessian)));
    }

    /// <summary>
    /// The Newton step, halved until the log-posterior does not fall and β stays above 0.05: a full step can overshoot far from the optimum
    /// (seen with two squads far apart on the scale), and then the clamp on β used to settle on a wrong answer.
    /// </summary>
    static double[] Advance(List<(double[] D, double Score)> rows, double[] x, double[] delta)
    {
        double before = LogPosterior(rows, x);
        for (double t = 1; t > 1e-6; t /= 2)
        {
            var next = x.Select((v, i) => v + t * delta[i]).ToArray();
            if (next[^1] > 0.05 && LogPosterior(rows, next) >= before - 1e-12) return next;
        }

        return x;
    }

    /// <summary>
    /// The log-posterior at <paramref name="x"/> (up to a constant).
    /// </summary>
    static double LogPosterior(List<(double[] D, double Score)> rows, double[] x)
    {
        double sum = 0;
        foreach (var (d, score) in rows)
        {
            double m = Dot(d, x);
            sum += score * LogSigmoid(m) + (1 - score) * LogSigmoid(-m);
        }

        for (int i = 0; i < x.Length - 1; i++) sum -= ParameterPrior / 2 * x[i] * x[i];
        return sum - BetaPrior / 2 * (x[^1] - BetaCentre) * (x[^1] - BetaCentre);
    }

    /// <summary>
    /// The chance a side of strength <paramref name="a"/> beats one of strength <paramref name="b"/>.
    /// </summary>
    public static double WinChance(double a, double b) => Sigmoid(a - b);

    /// <summary>
    /// Each counted fight as its margin's coefficients (side 0 minus side 1) and side 0's score.
    /// </summary>
    static List<(double[] D, double Score)> Rows(IReadOnlyList<FightRecord> fights, int n, Func<string, IReadOnlyList<int>?> featuresOf)
    {
        var rows = new List<(double[], double)>();
        foreach (var fight in fights)
        {
            var d = new double[n];
            if (Add(fight.Sides[0], +1, featuresOf, d) && Add(fight.Sides[1], -1, featuresOf, d)) rows.Add((d, fight.Score(0)));
        }

        return rows;
    }

    /// <summary>
    /// Adds a side's coefficients: +1 on each of its features, or ln count on β for a yardstick. False when the squad is outside the fit.
    /// </summary>
    static bool Add(SideKey side, int sign, Func<string, IReadOnlyList<int>?> featuresOf, double[] d)
    {
        if (side.YardstickCount is { } count)
        {
            d[^1] += sign * Math.Log(count);
            return true;
        }

        if (featuresOf(side.Key) is not { } features) return false;

        foreach (int f in features) d[f] += sign;
        return true;
    }

    /// <summary>
    /// The gradient and Hessian of the log-posterior at <paramref name="x"/>.
    /// </summary>
    static (double[] Gradient, double[,] Hessian) Derivatives(List<(double[] D, double Score)> rows, double[] x)
    {
        int n = x.Length;
        var g = new double[n];
        var h = new double[n, n];
        foreach (var (d, score) in rows)
        {
            double p = Sigmoid(Dot(d, x)), residual = score - p, weight = p * (1 - p);
            for (int i = 0; i < n; i++)
            {
                if (d[i] == 0) continue;

                g[i] += residual * d[i];
                for (int j = 0; j < n; j++) h[i, j] -= weight * d[i] * d[j];
            }
        }

        AddPrior(x, g, h);
        return (g, h);
    }

    /// <summary>
    /// The priors' gradient and curvature.
    /// </summary>
    static void AddPrior(double[] x, double[] g, double[,] h)
    {
        int n = x.Length;
        for (int i = 0; i < n - 1; i++)
        {
            g[i] -= ParameterPrior * x[i];
            h[i, i] -= ParameterPrior;
        }

        g[^1] -= BetaPrior * (x[^1] - BetaCentre);
        h[n - 1, n - 1] -= BetaPrior;
    }

    #region Linear algebra

    /// <summary>
    /// The logistic function.
    /// </summary>
    static double Sigmoid(double v) => 1 / (1 + Math.Exp(-v));

    /// <summary>
    /// ln σ(v), without overflow.
    /// </summary>
    static double LogSigmoid(double v) => v >= 0 ? -Math.Log(1 + Math.Exp(-v)) : v - Math.Log(1 + Math.Exp(v));

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
