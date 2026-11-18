using Meitou.Content;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Data.Save;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Items;
using SquadArena;

namespace SquadRating;

/// <summary>
/// Writes a squad file from one of the player's squads in a save (docs/squad-rating.md "Capture"): each member's character record, race, worn
/// gear (a weapon with its exact maker, model and level, armour with its quality) and every stat as saved. Backpacks, prosthetic limbs and what
/// the grids hold are left out.
/// </summary>
static class SquadCapture
{
    /// <summary>
    /// The squad named <paramref name="squad"/> (any case) of the player's in the save named <paramref name="save"/>.
    /// </summary>
    public static SquadFile Capture(GameInstall install, GameDatabase db, string save, string squad)
    {
        var game = SaveGame.Load(FindSave(install, save));
        string player = game.PlayerFaction?.Id ?? throw new UsageException($"save '{save}' has no player faction");
        var squads = game.Platoons.Where(p => p.FactionId == player && p.IsLoaded && p.Characters.Count > 0).ToList();
        var platoon = squads.FirstOrDefault(p => p.Record.Name.Equals(squad, StringComparison.OrdinalIgnoreCase))
            ?? throw new UsageException($"no squad '{squad}' in '{save}'; its squads: {string.Join(", ", squads.Select(p => $"\"{p.Record.Name}\""))}");

        return new SquadFile { Name = platoon.Record.Name, Members = [.. platoon.Characters.Select(c => Member(db, platoon, c))] };
    }

    /// <summary>
    /// The save's folder: the first save root (Meitou's own first) holding a folder of that name; a path is taken as it is.
    /// </summary>
    static string FindSave(GameInstall install, string save)
    {
        if (Directory.Exists(save)) return save;

        return SaveModList.Roots(install)
            .Select(root => Path.Combine(root.Folder, save))
            .FirstOrDefault(Directory.Exists) ?? throw new UsageException($"no save named '{save}' in Meitou's or Kenshi's save folders");
    }

    /// <summary>
    /// One member as a squad file line.
    /// </summary>
    static SideMember Member(GameDatabase db, SavePlatoon platoon, SaveCharacter c) => new()
    {
        Name = c.Name,
        Character = c.RecordId,
        Race = c.Appearance is { } a && a.References.TryGetValue("race", out var race) && race.Count > 0 ? race[0].TargetStringId : null,
        Gear = Gear(db, platoon, c),
        Stats = Stats(c),
    };

    /// <summary>
    /// The worn gear: the weapons, armour, crossbows and items outside the grids and the backpack slot (prosthetic limbs are other records).
    /// </summary>
    static GearItem[] Gear(GameDatabase db, SavePlatoon platoon, SaveCharacter c)
    {
        if (c.Inventory is not { } inventory || platoon.File is not { } file) return [];

        return [.. SaveInventory.Read(file, inventory)
            .Where(i => !InventoryCommands.IsGridSection(i.Section) && i.Section != "backpack_attach")
            .Select(i => (Item: i, Type: db.Find(i.BaseId)?.Type))
            .Where(i => i.Type is FcsRecordType.Weapon or FcsRecordType.Armour or FcsRecordType.Crossbow or FcsRecordType.Item)
            .Select(i => GearOf(i.Item, i.Type == FcsRecordType.Weapon))];
    }

    /// <summary>
    /// A worn item: a weapon's maker, model and level, anything else's quality.
    /// </summary>
    static GearItem GearOf(SaveItem item, bool weapon) => weapon
        ? new GearItem { Item = item.BaseId, Maker = NullIfEmpty(item.CompanyId), Model = NullIfEmpty(item.MaterialId), Level = item.Level }
        : new GearItem { Item = item.BaseId, Quality = (int)MathF.Round(item.Quality) };

    /// <summary>
    /// Every stat the save holds for the character, by name, rounded to a tenth.
    /// </summary>
    static Dictionary<string, float> Stats(SaveCharacter c)
    {
        if (c.Stats is not { } record) return [];

        var stats = CharacterStats.ReadSave(record.Floats);
        return StatsInfo.RecordStats.ToDictionary(s => s.ToString(), s => MathF.Round(stats.Stored(s), 1));
    }

    /// <summary>
    /// Null for an empty string.
    /// </summary>
    static string? NullIfEmpty(string text) => text.Length > 0 ? text : null;
}
