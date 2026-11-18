using System.Text.Json;

namespace SquadArena;

/// <summary>
/// The arena's file exchange (docs/squad-rating.md "Request and result"): SquadRating drops <c>inbox/&lt;id&gt;.json</c>, the arena takes the
/// oldest by name and deletes it, and writes <c>results/&lt;id&gt;.json</c> when the fight ends (through a temporary file, so a reader never sees a
/// half-written one).
/// </summary>
public sealed class ArenaFolders(string root)
{
    /// <summary>
    /// The folder that holds both.
    /// </summary>
    public string Root { get; } = root;

    /// <summary>
    /// Where requests are dropped.
    /// </summary>
    public string Inbox => Path.Combine(Root, "inbox");

    /// <summary>
    /// Where results are written.
    /// </summary>
    public string Results => Path.Combine(Root, "results");

    /// <summary>
    /// The result file of a fight.
    /// </summary>
    public string ResultOf(string id) => Path.Combine(Results, id + ".json");

    /// <summary>
    /// Drops a request (SquadRating's side).
    /// </summary>
    public void Drop(FightRequest request)
    {
        Directory.CreateDirectory(Inbox);
        WriteAtomically(Path.Combine(Inbox, request.Id + ".json"), JsonSerializer.Serialize(request, FightRequest.Json));
    }

    /// <summary>
    /// Takes the oldest request by name, deleting its file; null when the inbox is empty. A file that does not parse comes back as its id with the
    /// error.
    /// </summary>
    public (string Id, FightRequest? Request, string? Error)? Take()
    {
        if (!Directory.Exists(Inbox)) return null;
        if (Directory.EnumerateFiles(Inbox, "*.json").Order(StringComparer.Ordinal).FirstOrDefault() is not { } path) return null;

        string id = Path.GetFileNameWithoutExtension(path);
        try
        {
            string text = File.ReadAllText(path);
            File.Delete(path);
            return (id, JsonSerializer.Deserialize<FightRequest>(text, FightRequest.Json), null);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            TryDelete(path);
            return (id, null, e.Message);
        }
    }

    /// <summary>
    /// Writes a fight's result.
    /// </summary>
    public void Write(FightResult result)
    {
        Directory.CreateDirectory(Results);
        WriteAtomically(ResultOf(result.Id), JsonSerializer.Serialize(result, FightRequest.Json));
    }

    /// <summary>
    /// Reads a fight's result; null while there is none.
    /// </summary>
    public FightResult? Read(string id) =>
        File.Exists(ResultOf(id)) ? JsonSerializer.Deserialize<FightResult>(File.ReadAllText(ResultOf(id)), FightRequest.Json) : null;

    /// <summary>
    /// Writes a file under a temporary name, then renames it into place.
    /// </summary>
    static void WriteAtomically(string path, string text)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Deletes a file, ignoring a failure.
    /// </summary>
    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
