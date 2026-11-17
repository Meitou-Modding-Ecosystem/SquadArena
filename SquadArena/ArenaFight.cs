using System.Numerics;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Combat;

namespace SquadArena;

/// <summary>
/// A fight under way (docs/squad-rating.md "How a fight runs"). The fighters stand still for <see cref="SettleTicks"/> while their kit is applied;
/// then, once a tick, every standing fighter without a target is ordered at the nearest standing enemy, and once a second one circling a crowded
/// target turns on the nearest enemy with room, as the game's own --battle drives its blocks. It ends the first tick a side has nobody standing, or
/// as a draw at the request's timeout. Afterwards every fighter is killed, so nobody from it wakes up next to the next fight.
/// </summary>
sealed class ArenaFight
{
    /// <summary>
    /// Ticks between the spawn and the first order: the kit is applied on the first, the fighters stand a moment.
    /// </summary>
    public const int SettleTicks = 30;

    /// <summary>
    /// How often a fighter its target left out looks for another (a second).
    /// </summary>
    const int RetargetTicks = 30;

    /// <summary>
    /// The request.
    /// </summary>
    public FightRequest Request { get; }

    /// <summary>
    /// Each side's fighters, as spawned.
    /// </summary>
    readonly List<CharacterId>[] _sides;

    /// <summary>
    /// Every fighter, side 0's first: the log's numbering.
    /// </summary>
    readonly List<CharacterId> _all;

    /// <summary>
    /// The tick the first orders go out.
    /// </summary>
    readonly long _startTick;

    /// <summary>
    /// The log, made when the first orders go out.
    /// </summary>
    FightLog? _log;

    /// <summary>
    /// The fighters as spawned, read when the first orders go out (their kit is on by then).
    /// </summary>
    FighterInfo[] _fighters = [];

    /// <summary>
    /// A fight of <paramref name="sides"/>, spawned at tick <paramref name="spawnTick"/>.
    /// </summary>
    public ArenaFight(FightRequest request, List<CharacterId>[] sides, long spawnTick)
    {
        Request = request;
        _sides = sides;
        _all = [.. sides[0], .. sides[1]];
        _startTick = spawnTick + SettleTicks;
    }

    /// <summary>
    /// One tick of the fight; the result once it is over, else null.
    /// </summary>
    public FightResult? Tick(World world, CombatSystem combat)
    {
        if (world.Tick < _startTick) return null;
        if (_log is null) Begin(world);

        _log!.Watch(world);
        var standing = _sides.Select(side => side.Where(id => Standing(world, id)).ToList()).ToArray();
        float seconds = (world.Tick - _startTick) * world.TickSeconds;
        if (standing[0].Count == 0 || standing[1].Count == 0) return End(world, standing, seconds, timedOut: false);
        if (seconds >= Request.TimeoutSeconds) return End(world, standing, seconds, timedOut: true);

        Order(world, combat, standing);
        if ((world.Tick - _startTick) % RetargetTicks == 0) Retarget(world, combat, standing);
        return null;
    }

    /// <summary>
    /// Records a landed hit when the fight has begun.
    /// </summary>
    public void Hit(World world, in CombatHit hit) => _log?.Hit(world, hit);

    /// <summary>
    /// The first ordering tick: the fighters' descriptions and the log's starting point.
    /// </summary>
    void Begin(World world)
    {
        _log = new FightLog(_all, _startTick, world.TickSeconds);
        _log.Start(world);
        _fighters = [.. _all.Select((id, i) => Describe(world, id, i, i < _sides[0].Count ? 0 : 1))];
    }

    /// <summary>
    /// A fighter as it stands at the first order.
    /// </summary>
    static FighterInfo Describe(World world, CharacterId id, int index, int side)
    {
        if (!world.Characters.TryResolveNext(id, out int slot) || world.Characters.Cold(slot) is not { } cold)
            return new FighterInfo(index, side, "", "", null, null);

        return new FighterInfo(index, side, cold.Name, cold.RecordId, cold.Race?.StringId,
            FightLog.WeaponOf(world, id, CombatHitSource.Melee) is { } weapon and not "unarmed" ? weapon : null);
    }

    #region Orders

    /// <summary>
    /// Orders every standing fighter without a target at the nearest standing enemy.
    /// </summary>
    static void Order(World world, CombatSystem combat, List<CharacterId>[] standing)
    {
        for (int side = 0; side < 2; side++)
            foreach (var id in standing[side])
            {
                if (combat.StateOf(id).TargetSlot >= 0) continue;

                var here = Ground(world, id);
                var foe = standing[1 - side].MinBy(e => Vector2.DistanceSquared(here, Ground(world, e)));
                Attack(world, id, foe);
            }
    }

    /// <summary>
    /// A fighter circling a target with more than <see cref="CombatSystem.MaxEngaged"/> after it turns on the nearest standing enemy with room.
    /// </summary>
    static void Retarget(World world, CombatSystem combat, List<CharacterId>[] standing)
    {
        var after = Crowding(combat, standing);
        for (int side = 0; side < 2; side++)
            foreach (var id in standing[side])
            {
                var state = combat.StateOf(id);
                if (state.TargetSlot < 0 || state.Engaged || state.Ranged || state.AttackTech >= 0
                    || after.GetValueOrDefault(state.TargetSlot) <= CombatSystem.MaxEngaged) continue;
                if (RoomyFoe(world, id, standing[1 - side], state.TargetSlot, after) is not { } foe) continue;

                after[state.TargetSlot]--;
                after[foe.Slot] = after.GetValueOrDefault(foe.Slot) + 1;
                Attack(world, id, foe);
            }
    }

    /// <summary>
    /// How many standing melee fighters are after each slot.
    /// </summary>
    static Dictionary<int, int> Crowding(CombatSystem combat, List<CharacterId>[] standing)
    {
        var after = new Dictionary<int, int>();
        foreach (var side in standing)
            foreach (var id in side)
                if (combat.StateOf(id) is { TargetSlot: >= 0 and var t, Down: false, Ranged: false }) after[t] = after.GetValueOrDefault(t) + 1;

        return after;
    }

    /// <summary>
    /// The nearest enemy, other than the current target, with fewer than <see cref="CombatSystem.MaxEngaged"/> after it.
    /// </summary>
    static CharacterId? RoomyFoe(World world, CharacterId id, List<CharacterId> enemies, int current, Dictionary<int, int> after)
    {
        var here = Ground(world, id);
        return enemies
            .Where(e => e.Slot != current && after.GetValueOrDefault(e.Slot) < CombatSystem.MaxEngaged)
            .Select(e => (CharacterId?)e)
            .MinBy(e => Vector2.DistanceSquared(here, Ground(world, e!.Value)));
    }

    /// <summary>
    /// Queues an attack order for the next tick, as an NPC's own.
    /// </summary>
    static void Attack(World world, CharacterId attacker, CharacterId target) =>
        world.Defer(new AttackOrder([attacker], target) { Tick = world.Tick + 1, Source = TaskSource.Npc });

    #endregion

    #region End

    /// <summary>
    /// Writes the result and kills every fighter.
    /// </summary>
    FightResult End(World world, List<CharacterId>[] standing, float seconds, bool timedOut)
    {
        _log!.Watch(world);
        int winner = timedOut ? -1 : standing[0].Count > 0 ? 0 : standing[1].Count > 0 ? 1 : -1;
        var result = new FightResult
        {
            Id = Request.Id,
            Winner = winner,
            Outcome = timedOut ? "timeout" : "decided",
            Seconds = MathF.Round(seconds, 2),
            Sides = [Outcome(world, 0, standing[0].Count), Outcome(world, 1, standing[1].Count)],
            Fighters = _fighters,
            Log = [.. _log.Events],
        };
        world.Defer(new DebugKill([.. _all.Where(id => world.Characters.IsAlive(id))]) { Tick = world.Tick + 1 });
        return result;
    }

    /// <summary>
    /// A side's count at the end.
    /// </summary>
    SideOutcome Outcome(World world, int side, int standing)
    {
        int first = side == 0 ? 0 : _sides[0].Count;
        int alive = _sides[side].Count(id => world.Characters.TryResolveNext(id, out int slot)
            && world.Characters.Cold(slot)?.Medical is { Dead: false });
        float dealt = Enumerable.Range(first, _sides[side].Count).Sum(_log!.Dealt);
        return new SideOutcome(Request.Sides[side].Name, _sides[side].Count, standing, alive, MathF.Round(dealt, 1));
    }

    #endregion

    /// <summary>
    /// Whether the fighter is in the world and not incapacitated.
    /// </summary>
    static bool Standing(World world, CharacterId id) =>
        world.Characters.TryResolveNext(id, out int slot) && world.Characters.Cold(slot)?.Medical is { Incapacitated: false };

    /// <summary>
    /// The fighter's position on the ground plane.
    /// </summary>
    static Vector2 Ground(World world, CharacterId id)
    {
        var at = world.Characters.Previous[id.Slot].Position;
        return new Vector2(at.X, at.Z);
    }
}
