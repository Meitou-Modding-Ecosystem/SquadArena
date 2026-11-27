namespace SquadRating.Tests;

/// <summary>
/// <c>--shard k/n</c> splits a price sheet's fighters between machines (docs/squad-rating.md "Price lists").
/// </summary>
public sealed class ShardTests
{
    [Fact]
    public void The_shards_of_a_sheet_take_every_fighter_exactly_once()
    {
        var shards = Enumerable.Range(1, 6).Select(k => Shard.Parse($"{k}/6")).ToList();

        foreach (int fighter in Enumerable.Range(0, 31)) Assert.Single(shards, s => s.Includes(fighter));
        Assert.Equal([1, 7, 13, 19, 25], Enumerable.Range(0, 31).Where(shards[1].Includes));
    }

    [Fact]
    public void No_shard_takes_every_fighter()
    {
        Assert.All(Enumerable.Range(0, 31), i => Assert.True(Shard.Parse(null).Includes(i)));
    }

    [Theory]
    [InlineData("0/6")]
    [InlineData("7/6")]
    [InlineData("2")]
    [InlineData("a/b")]
    public void A_bad_shard_is_a_usage_error(string text)
    {
        Assert.Throws<UsageException>(() => Shard.Parse(text));
    }
}
