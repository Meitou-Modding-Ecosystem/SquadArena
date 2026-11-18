using Meitou.Modding;
using Meitou.Simulation;

namespace SquadArena;

/// <summary>
/// SquadArena's entry (docs/squad-rating.md): registers the arena system, which fights the requests SquadRating drops into the mod's
/// <c>arena/inbox</c> folder.
/// </summary>
public sealed class ArenaMod : IMod
{
    /// <summary>
    /// The mod's manifest id.
    /// </summary>
    public const string Id = "squad.arena";

    public void Initialize(ModContext c)
    {
        var folders = new ArenaFolders(Path.Combine(c.Folder, "arena"));
        // A factory that returns null leaves its system out of that world (docs/modding.md "Tick systems").
        c.AddSystem("squad.arena", ctx => ArenaSystem.Create(ctx, folders, c.Log, c.LogError)!, SystemAnchor.End);
        c.Log($"SquadArena: fights requests from {folders.Inbox}");
    }
}
