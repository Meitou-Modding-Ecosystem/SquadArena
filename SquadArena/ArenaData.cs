using Meitou.Data.Fcs;

namespace SquadArena;

/// <summary>
/// The mod's data file (docs/squad-rating.md "The arena factions"): two factions, Arena Red and Arena Blue, enemies of each other and neutral to
/// everyone else, with no towns and no roaming squads. The fighters of each side join one of them, so each side's senses take the other as hostile
/// and nobody else does. Holds no game content, so SquadRating writes it without an install.
/// </summary>
public static class ArenaData
{
    /// <summary>
    /// The data file's name; a record's id ends with it.
    /// </summary>
    public const string FileName = "SquadArena.mod";

    /// <summary>
    /// The factions' record ids, side 0 then side 1.
    /// </summary>
    public static readonly string[] Factions = ["1-" + FileName, "2-" + FileName];

    /// <summary>
    /// The factions' names, side 0 then side 1.
    /// </summary>
    public static readonly string[] FactionNames = ["Arena Red", "Arena Blue"];

    /// <summary>
    /// The relation each arena faction has with the other: the lowest there is.
    /// </summary>
    public const int Enmity = -100;

    /// <summary>
    /// Builds the data file.
    /// </summary>
    public static FcsFile Build()
    {
        var file = new FcsFile { Author = "MeitouMods", Description = "SquadArena: the two arena factions. Generated; see docs/squad-rating.md." };
        for (int side = 0; side < 2; side++) file.Records.Add(Faction(side));
        for (int k = 0; k < file.Records.Count; k++) file.Records[k].Id = k + 1;

        file.NextId = file.Records.Count + 1;
        return file;
    }

    /// <summary>
    /// One arena faction: neutral by default, an enemy of the other side's.
    /// </summary>
    static FcsRecord Faction(int side)
    {
        var record = new FcsRecord { StringId = Factions[side], Flags = 0x10, Name = FactionNames[side], RecordType = FcsRecordType.Faction };
        record.Ints["default relation"] = 0;
        record.Ints["roaming population"] = 0;
        record.References["relations"] = [new FcsReference(Factions[1 - side], Enmity, 0, 0)];
        return record;
    }
}
