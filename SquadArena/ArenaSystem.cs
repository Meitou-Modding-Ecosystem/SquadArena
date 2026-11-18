using Meitou.Simulation;
using Meitou.Simulation.Combat;
using Meitou.Simulation.Population;

namespace SquadArena;

/// <summary>
/// The arena (docs/squad-rating.md "How a fight runs"): while no fight is under way it looks in the inbox every <see cref="PollTicks"/>, spawns
/// the next request's sides and runs the fight to its end, then writes the result. One fight at a time. Test only: it reads files from the tick,
/// so a world running it is not deterministic, and nothing of it is saved.
/// </summary>
sealed class ArenaSystem(PopulationSystem population, CombatSystem combat, int[] factions, ArenaFolders folders, Action<string> log,
    Action<string> logError) : ICombatHitObserver
{
    /// <summary>
    /// Ticks between looks in the inbox while idle (a second).
    /// </summary>
    public const int PollTicks = 30;

    /// <summary>
    /// The fight under way; null while idle.
    /// </summary>
    ArenaFight? _fight;

    /// <summary>
    /// The system for a new world; null (and a log line) when the world lacks the population or combat system.
    /// </summary>
    public static ArenaSystem? Create(SystemBuildContext ctx, ArenaFolders folders, Action<string> log, Action<string> logError)
    {
        if (ctx.Find<PopulationSystem>() is not { } population || ctx.Find<CombatSystem>() is not { } combat)
        {
            logError("SquadArena: this world has no population or combat system; the arena is off");
            return null;
        }

        int[] factions = [.. ArenaData.Factions.Select(ctx.Data.FactionIndex)];
        return new ArenaSystem(population, combat, factions, folders, log, logError);
    }

    public void SlowWorld(World world)
    {
        if (_fight is not null)
        {
            Advance(world);
            return;
        }

        if (world.Tick % PollTicks == 0) TakeNext(world);
    }

    public void HitResolved(World world, in CombatHit hit) => _fight?.Hit(world, hit);

    /// <summary>
    /// Ticks the fight; writes its result once it is over.
    /// </summary>
    void Advance(World world)
    {
        if (_fight!.Tick(world, combat) is not { } result) return;

        _fight = null;
        Write(result);
        log($"SquadArena: fight {result.Id}: {Describe(result)}");
    }

    /// <summary>
    /// Takes the next request and starts it; a request that can't be fought gets a refused result at once.
    /// </summary>
    void TakeNext(World world)
    {
        if (folders.Take() is not var (id, request, error)) return;
        if (request is null) error ??= "the request is empty";
        if (error is null && factions.Any(f => f < 0)) error = "the arena factions are missing: SquadArena.mod is not loaded";

        var spawner = new FightSpawner(population, population.Data.Db);
        var plan = error is null ? spawner.Plan(request!) : ([], error);
        if (plan.Error is { } refused)
        {
            Write(new FightResult { Id = id, Outcome = "refused", Error = refused });
            logError($"SquadArena: fight {id} refused: {refused}");
            return;
        }

        var sides = spawner.Spawn(world, request! with { Id = id }, plan.Sides, factions);
        _fight = new ArenaFight(request! with { Id = id }, sides, world.Tick);
    }

    /// <summary>
    /// Writes a result, logging a failure instead of throwing from the tick.
    /// </summary>
    void Write(FightResult result)
    {
        try
        {
            folders.Write(result);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logError($"SquadArena: could not write the result of {result.Id}: {e.Message}");
        }
    }

    /// <summary>
    /// A result as one log line.
    /// </summary>
    static string Describe(FightResult result) => result.Winner < 0
        ? $"draw ({result.Outcome}) after {result.Seconds:0.0} s"
        : $"{result.Sides[result.Winner].Name} win, {result.Sides[result.Winner].Standing} standing, after {result.Seconds:0.0} s";

    public void Hash(ref StateHasher hasher) { }
}
