namespace SquadRating.Tests;

/// <summary>
/// The ranking fit (docs/squad-rating.md "The yardstick") recovers ratings from fights drawn with known strengths.
/// </summary>
public sealed class RatingModelTests
{
    [Fact]
    public void A_squad_worth_four_bandits_is_rated_about_four_with_the_truth_in_its_range()
    {
        var squad = new SideKey("squad:a#1", "A");
        var fights = MadeUpFights.AgainstYardstick(squad, MadeUpFights.StrengthOf(4), 600, new Random(7)).ToList();

        var (ratings, beta) = RatingModel.Fit(fights);

        var a = Assert.Single(ratings);
        Assert.InRange(a.Rating, 3.3, 4.8);
        Assert.InRange(4, a.Low, a.High);
        Assert.InRange(beta, 1.1, 1.9);
        Assert.Equal(600, a.Fights);
    }

    [Fact]
    public void Squads_that_never_met_are_ranked_through_the_yardstick()
    {
        var random = new Random(3);
        var weak = new SideKey("squad:weak#1", "Weak");
        var strong = new SideKey("squad:strong#1", "Strong");
        var fights = MadeUpFights.AgainstYardstick(weak, MadeUpFights.StrengthOf(1.5), 400, random)
            .Concat(MadeUpFights.AgainstYardstick(strong, MadeUpFights.StrengthOf(6), 400, random))
            .ToList();

        var (ratings, _) = RatingModel.Fit(fights);

        Assert.Equal(["Strong", "Weak"], ratings.Select(r => r.Name));
        Assert.InRange(ratings[0].Rating, 4.5, 8);
        Assert.InRange(ratings[1].Rating, 1.1, 2.0);
    }
}
