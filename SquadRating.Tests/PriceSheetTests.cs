using SquadArena;

namespace SquadRating.Tests;

/// <summary>
/// A price sheet's variants (docs/squad-rating.md "Price lists"): the baseline, one fighter per option with only that change, and mixes.
/// </summary>
public sealed class PriceSheetTests
{
    /// <summary>
    /// A katana-armed Dust Bandit in a shirt, a race, a weapon swap, armour off and on, a stat option and a two-level sweep.
    /// </summary>
    static PriceSheet Sheet() => new()
    {
        Name = "Melee",
        Baseline = new PriceFighter
        {
            Character = "Dust Bandit",
            Gear = new() { ["weapon"] = new GearItem { Item = "Katana", Level = 30 }, ["body"] = new GearItem { Item = "Shirt", Quality = 50 } },
            Stats = new() { ["katanas"] = 30, ["toughness"] = 30 },
        },
        Options =
        [
            new PriceOption { Name = "Shek", Race = "Shek" },
            new PriceOption { Name = "Nodachi", Gear = new() { ["weapon"] = new GearItem { Item = "Nodachi", Level = 30 } } },
            new PriceOption { Name = "No shirt", Gear = new() { ["body"] = null } },
            new PriceOption { Name = "Tough", Stats = new() { ["Toughness"] = 60 } },
        ],
        StatSweeps = [new StatSweep { Name = "skill", Stats = ["katanas", "melee attack"], Levels = [50, 70] }],
    };

    [Fact]
    public void Each_option_makes_one_fighter_with_only_that_change()
    {
        var variants = Sheet().Variants(0);

        Assert.Equal(["baseline", "Shek", "Nodachi", "No shirt", "Tough", "skill 50", "skill 70"], variants.Select(v => v.Label));
        var baseline = variants[0].Squad.Members[0];
        Assert.Null(baseline.Race);
        Assert.Equal(["Shirt", "Katana"], baseline.Gear!.Select(g => g.Item));
        Assert.Equal("Shek", variants[1].Squad.Members[0].Race);
        Assert.Equal(["Shirt", "Nodachi"], variants[2].Squad.Members[0].Gear!.Select(g => g.Item));
        Assert.Equal(["Katana"], variants[3].Squad.Members[0].Gear!.Select(g => g.Item));
        Assert.Equal(60, variants[4].Squad.Members[0].Stats!["toughness"]);
        Assert.Equal(2, variants[4].Squad.Members[0].Stats!.Count);
        Assert.Equal(70, variants[6].Squad.Members[0].Stats!["melee attack"]);
        Assert.Equal(30, variants[6].Squad.Members[0].Stats!["toughness"]);
        Assert.Equal("Melee: Nodachi", variants[2].Squad.Name);
    }

    [Fact]
    public void Options_without_a_group_are_grouped_by_what_they_change()
    {
        var groups = Sheet().AllOptions().Select(o => o.Group);

        Assert.Equal(["race", "gear: weapon", "gear: body", "stats: Toughness", "skill", "skill"], groups);
    }

    [Fact]
    public void Mixes_take_one_option_from_each_of_two_or_more_groups_and_are_the_same_every_time()
    {
        var sheet = Sheet();
        var options = sheet.AllOptions();

        var mixes = sheet.Variants(8).Skip(1 + options.Count).ToList();

        Assert.Equal(8, mixes.Count);
        foreach (var mix in mixes)
        {
            var groups = mix.Options.Select(i => options[i].Group).ToList();
            Assert.True(groups.Count >= 2);
            Assert.Equal(groups.Count, groups.Distinct().Count());
        }

        Assert.Equal(mixes.Select(m => m.Label), sheet.Variants(8).Skip(1 + options.Count).Select(m => m.Label));
        Assert.Equal(mixes.Count, mixes.Select(m => m.Label).Distinct().Count());
    }

    [Fact]
    public void A_baseline_with_no_gear_fights_bare_handed()
    {
        var sheet = Sheet() with { Baseline = new PriceFighter { Character = "Dust Bandit" }, Options = [], StatSweeps = [] };

        var member = Assert.Single(sheet.Variants(0)).Squad.Members[0];

        Assert.Empty(member.Gear!);
        Assert.Null(member.Stats);
    }
}
