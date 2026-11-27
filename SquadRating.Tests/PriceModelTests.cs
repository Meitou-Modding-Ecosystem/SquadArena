using SquadArena;

namespace SquadRating.Tests;

/// <summary>
/// The price fit (docs/squad-rating.md "Price lists") recovers what each option adds from fights drawn with known effects.
/// </summary>
public sealed class PriceModelTests
{
    /// <summary>
    /// A baseline worth 2 bandits; Shek multiplies it by 1.5, a nodachi by 2.
    /// </summary>
    static readonly double Base = MadeUpFights.StrengthOf(2), Shek = MadeUpFights.StrengthOf(1.5), Nodachi = MadeUpFights.StrengthOf(2);

    /// <summary>
    /// The sheet: a Dust Bandit, Shek and a nodachi.
    /// </summary>
    static PriceSheet Sheet() => new()
    {
        Name = "Test",
        Baseline = new PriceFighter { Character = "Dust Bandit" },
        Options =
        [
            new PriceOption { Name = "Shek", Race = "Shek" },
            new PriceOption { Name = "Nodachi", Gear = new() { ["weapon"] = new GearItem { Item = "Nodachi" } } },
        ],
    };

    /// <summary>
    /// Each variant's key: its label stands in for the resolved hash.
    /// </summary>
    static Dictionary<string, int[]> Keys(IEnumerable<PriceVariant> variants) => variants.ToDictionary(v => "squad:" + v.Label, v => v.Options);

    /// <summary>
    /// 400 fights of each variant against the yardstick, drawn with the true effects.
    /// </summary>
    static List<FightRecord> Fights(Dictionary<string, int[]> keys, int seed)
    {
        var random = new Random(seed);
        double[] effects = [Shek, Nodachi];
        return [.. keys.SelectMany(k => MadeUpFights.AgainstYardstick(new SideKey(k.Key, k.Key), Base + k.Value.Sum(i => effects[i]), 400, random))];
    }

    [Fact]
    public void Each_option_is_priced_as_what_it_adds_to_the_baseline_in_points()
    {
        var sheet = Sheet();
        var keys = Keys(sheet.Variants(1));

        var prices = PriceModel.Fit(sheet, keys, Fights(keys, 11));

        Assert.Equal(["baseline", "Shek", "Nodachi"], prices.Select(p => p.Name));
        Assert.InRange(prices[0].Points, 170, 235);
        Assert.InRange(prices[1].Points, 60, 145);
        Assert.InRange(prices[2].Points, 140, 270);
        Assert.InRange(prices[2].Factor, 1.6, 2.5);
        Assert.All(prices, p => Assert.True(p.Low < p.Points && p.Points < p.High));
    }

    [Fact]
    public void The_price_range_holds_the_truth()
    {
        var sheet = Sheet();
        var keys = Keys(sheet.Variants(1));

        var prices = PriceModel.Fit(sheet, keys, Fights(keys, 5));

        Assert.InRange(200, prices[0].Low, prices[0].High);
        Assert.InRange(100, prices[1].Low, prices[1].High);
        Assert.InRange(200, prices[2].Low, prices[2].High);
    }

    [Fact]
    public void Fights_of_other_squads_are_left_out()
    {
        var sheet = Sheet();
        var keys = Keys(sheet.Variants(0));
        var fights = Fights(keys, 2);
        var other = new SideKey("squad:other#1", "Other");
        var noise = MadeUpFights.AgainstYardstick(other, MadeUpFights.StrengthOf(9), 200, new Random(4));

        var alone = PriceModel.Fit(sheet, keys, fights);
        var mixed = PriceModel.Fit(sheet, keys, [.. fights, .. noise]);

        Assert.Equal(alone.Select(p => p.Fights), mixed.Select(p => p.Fights));
        Assert.Equal(alone[1].Points, mixed[1].Points, 0);
    }

    [Fact]
    public void Price_list_fights_are_counted_per_option()
    {
        var sheet = Sheet();
        var keys = Keys(sheet.Variants(1));

        var prices = PriceModel.Fit(sheet, keys, Fights(keys, 9));

        Assert.Equal(400, prices[0].Fights);
        Assert.Equal(800, prices[1].Fights);
        Assert.Equal(800, prices[2].Fights);
    }
}
