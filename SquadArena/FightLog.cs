using Meitou.Data.Fcs;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Combat;

namespace SquadArena;

/// <summary>
/// A fight's log (docs/squad-rating.md "The fight log"): every landed hit on or by a fighter, with attacker, weapon, body part and damage, from the
/// combat hit feed; and each fighter's downings, recoveries, death and lost limbs, found by comparing its medical state every tick.
/// </summary>
sealed class FightLog(IReadOnlyList<CharacterId> fighters, long startTick, float tickSeconds)
{
    /// <summary>
    /// The lines so far, in order.
    /// </summary>
    readonly List<FightEvent> _events = [];

    /// <summary>
    /// Each fighter's state when last looked at: down, dead, and per medical part its limb state.
    /// </summary>
    readonly (bool Down, bool Dead, LimbState[] Limbs)[] _seen = new (bool, bool, LimbState[])[fighters.Count];

    /// <summary>
    /// Damage each fighter delivered, summed over cut, blunt and pierce.
    /// </summary>
    readonly float[] _dealt = new float[fighters.Count];

    /// <summary>
    /// The lines so far.
    /// </summary>
    public IReadOnlyList<FightEvent> Events => _events;

    /// <summary>
    /// The damage fighter <paramref name="index"/> delivered.
    /// </summary>
    public float Dealt(int index) => _dealt[index];

    /// <summary>
    /// Takes every fighter's state as the starting point (spawned whole and standing).
    /// </summary>
    public void Start(World world)
    {
        for (int i = 0; i < fighters.Count; i++)
        {
            var medical = MedicalOf(world, fighters[i]);
            _seen[i] = (medical?.Incapacitated ?? false, medical?.Dead ?? false, [.. medical?.Parts.Select(p => p.Limb) ?? []]);
        }
    }

    /// <summary>
    /// Records a landed hit when it involves a fighter.
    /// </summary>
    public void Hit(World world, in CombatHit hit)
    {
        int victim = IndexOf(hit.Victim), attacker = IndexOf(hit.Attacker);
        if (victim < 0 && attacker < 0) return;

        var d = hit.Damage;
        var a = hit.Absorbed;
        if (attacker >= 0) _dealt[attacker] += (d.Cut + d.Blunt + d.Pierce) * d.Scale;
        _events.Add(new FightEvent
        {
            T = Seconds(world),
            Kind = "hit",
            Fighter = victim,
            By = attacker,
            Weapon = WeaponOf(world, hit.Attacker, hit.Source),
            Source = hit.Source == CombatHitSource.Projectile ? "projectile" : "melee",
            Part = PartName(world, hit.Victim, hit.Part),
            Damage = [Round(d.Cut * d.Scale), Round(d.Blunt * d.Scale), Round(d.Pierce * d.Scale)],
            Absorbed = [Round(a.Cut), Round(a.Blunt), Round(a.Pierce)],
            OnDowned = hit.VictimWasIncapacitated ? true : null,
        });
    }

    /// <summary>
    /// Compares every fighter's medical state with the last look and records what changed.
    /// </summary>
    public void Watch(World world)
    {
        for (int i = 0; i < fighters.Count; i++)
        {
            if (MedicalOf(world, fighters[i]) is not { } medical) continue;

            WatchLimbs(world, i, medical);
            WatchDown(world, i, medical);
        }
    }

    /// <summary>
    /// Records a fighter going down, getting up and dying.
    /// </summary>
    void WatchDown(World world, int i, MedicalState medical)
    {
        var (down, dead, limbs) = _seen[i];
        if (medical.Dead && !dead) Add(world, "dead", i);
        else if (medical.Incapacitated && !down) Add(world, "down", i);
        else if (!medical.Incapacitated && down) Add(world, "up", i);

        _seen[i] = (medical.Incapacitated, medical.Dead, limbs);
    }

    /// <summary>
    /// Records each limb newly severed or crushed.
    /// </summary>
    void WatchLimbs(World world, int i, MedicalState medical)
    {
        var limbs = _seen[i].Limbs;
        for (int p = 0; p < medical.Parts.Count && p < limbs.Length; p++)
        {
            var now = medical.Parts[p].Limb;
            if (now == limbs[p]) continue;

            limbs[p] = now;
            if (now is LimbState.Stump or LimbState.Crushed)
                _events.Add(new FightEvent { T = Seconds(world), Kind = now == LimbState.Stump ? "severed" : "crushed", Fighter = i,
                    Part = medical.Parts[p].Template.Name });
        }
    }

    /// <summary>
    /// Adds a line with no detail.
    /// </summary>
    void Add(World world, string kind, int fighter) => _events.Add(new FightEvent { T = Seconds(world), Kind = kind, Fighter = fighter });

    /// <summary>
    /// The fighter's number, or -1 for someone outside the fight.
    /// </summary>
    int IndexOf(CharacterId id)
    {
        for (int i = 0; i < fighters.Count; i++)
            if (fighters[i] == id) return i;

        return -1;
    }

    /// <summary>
    /// Game seconds since the fight's first order.
    /// </summary>
    float Seconds(World world) => Round((world.Tick - startTick) * tickSeconds);

    /// <summary>
    /// A number to two decimals, as the log keeps it.
    /// </summary>
    static float Round(float value) => MathF.Round(value, 2);

    /// <summary>
    /// The character's medical state; null once it has left the world.
    /// </summary>
    static MedicalState? MedicalOf(World world, CharacterId id) =>
        world.Characters.TryResolveNext(id, out int slot) ? world.Characters.Cold(slot)?.Medical : null;

    /// <summary>
    /// The name of the victim's medical part the hit chose.
    /// </summary>
    static string? PartName(World world, CharacterId victim, int part) =>
        MedicalOf(world, victim)?.Parts is { } parts && part >= 0 && part < parts.Count ? parts[part].Template.Name : null;

    /// <summary>
    /// What the attacker hit with: a bolt's crossbow, else the weapon in hand, else "unarmed".
    /// </summary>
    public static string? WeaponOf(World world, CharacterId attacker, CombatHitSource source)
    {
        if (!world.Characters.TryResolveNext(attacker, out int slot) || world.Characters.Cold(slot) is not { } cold) return null;

        var items = cold.Inventory?.Items ?? [];
        if (source == CombatHitSource.Projectile)
            return items.FirstOrDefault(i => i.Type == FcsRecordType.Crossbow && i.Section != "main" && i.Section != "backpack_content")?.Name;

        string hand = cold.Fighter is { Weapon: not null } f && f.WeaponIsBack ? "back" : "hip";
        return items.FirstOrDefault(i => i.Section == hand && i.Type == FcsRecordType.Weapon)?.Name
            ?? (cold.Fighter is { Weapon: null } ? "unarmed" : null);
    }
}
