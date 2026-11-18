using System.Diagnostics;
using SquadArena;

namespace SquadRating;

/// <summary>
/// A side ready to fight: its key and its resolved arena side.
/// </summary>
sealed record Contender(SideKey Key, SideRequest Side);

/// <summary>
/// Runs fights one at a time (docs/squad-rating.md "Running fights"): each at the next spot on a ring round the player's lead character, the arena
/// save loaded again before every <see cref="Settings.FightsPerLoad"/> fights; drops the request, waits for SquadArena's result and files it.
/// </summary>
sealed class FightRunner(GameSession game, ArenaFolders arena, FightHistory history, Settings settings)
{
    /// <summary>
    /// Fights run in this session (picks the spot and when to reload).
    /// </summary>
    int _count;

    /// <summary>
    /// The player's lead position after the last load.
    /// </summary>
    (float X, float Z) _centre;

    /// <summary>
    /// Fights <paramref name="a"/> (side 0) against <paramref name="b"/> (side 1) and files the result.
    /// </summary>
    public FightRecord Fight(Contender a, Contender b)
    {
        if (_count % settings.FightsPerLoad == 0) Load();

        var request = Request(a, b, _count++);
        arena.Drop(request);
        var result = AwaitResult(request);
        if (result.Outcome == "refused") throw new UsageException($"the arena refused the fight: {result.Error}");

        var record = new FightRecord(request.Id, [a.Key, b.Key], result.Winner, result.Outcome, result.Seconds);
        history.Add(record, result);
        Console.WriteLine($"  {a.Key.Name} vs {b.Key.Name}: {Describe(result)}");
        return record;
    }

    /// <summary>
    /// Loads the arena save again and reads where the player stands.
    /// </summary>
    void Load()
    {
        game.Reload();
        game.Run();
        _centre = game.LeadPosition();
    }

    /// <summary>
    /// The request for fight number <paramref name="n"/>: its spot is the next of <see cref="Settings.FightsPerLoad"/> on the ring.
    /// </summary>
    FightRequest Request(Contender a, Contender b, int n)
    {
        double angle = 2 * Math.PI * (n % settings.FightsPerLoad) / settings.FightsPerLoad;
        float x = _centre.X + settings.SpotDistance * (float)Math.Cos(angle), z = _centre.Z + settings.SpotDistance * (float)Math.Sin(angle);
        return new FightRequest
        {
            Id = $"{DateTime.UtcNow:yyyyMMddTHHmmss}-{n:D4}",
            At = [x, z],
            TimeoutSeconds = settings.TimeoutSeconds,
            Seed = (ulong)Random.Shared.NextInt64(),
            Sides = [a.Side, b.Side],
        };
    }

    /// <summary>
    /// Waits for the result: the fight's game time at speed 5, plus a minute for the spawn, before giving up.
    /// </summary>
    FightResult AwaitResult(FightRequest request)
    {
        var limit = TimeSpan.FromSeconds(60 + request.TimeoutSeconds / 5 * 2);
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < limit)
        {
            if (arena.Read(request.Id) is { } result) return result;
            Thread.Sleep(500);
        }

        throw new UsageException($"no result for fight {request.Id} within {limit.TotalSeconds:0} s: is SquadArena enabled and the game unpaused?");
    }

    /// <summary>
    /// A result in a few words.
    /// </summary>
    static string Describe(FightResult result) => result.Winner < 0
        ? $"draw ({result.Outcome}, {result.Seconds:0} s)"
        : $"{result.Sides[result.Winner].Name} won with {result.Sides[result.Winner].Standing} standing ({result.Seconds:0} s)";
}
