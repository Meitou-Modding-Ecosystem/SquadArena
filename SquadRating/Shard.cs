namespace SquadRating;

/// <summary>
/// <c>--shard k/n</c> (docs/squad-rating.md "Price lists"): a run fights only every n-th fighter from the k-th, so n machines can share a price
/// sheet and their fight lists be joined.
/// </summary>
/// <param name="Index">k − 1.</param>
/// <param name="Count">n.</param>
sealed record Shard(int Index, int Count)
{
    /// <summary>
    /// Every fighter: no <c>--shard</c>.
    /// </summary>
    public static readonly Shard All = new(0, 1);

    /// <summary>
    /// The shard <c>k/n</c> names (1 ≤ k ≤ n); <see cref="All"/> for null; a usage error otherwise.
    /// </summary>
    public static Shard Parse(string? text)
    {
        if (text is null) return All;

        var parts = text.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out int k) && int.TryParse(parts[1], out int n) && k >= 1 && k <= n)
            return new Shard(k - 1, n);

        throw new UsageException("--shard is k/n with 1 <= k <= n, e.g. 2/6");
    }

    /// <summary>
    /// Whether this shard fights the fighter at <paramref name="index"/> in the sheet's order.
    /// </summary>
    public bool Includes(int index) => index % Count == Index;
}
