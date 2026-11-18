using System.Diagnostics;
using System.Text.Json;

namespace SquadRating;

/// <summary>
/// A client of Meitou's agent control channel (engine docs/agent-playtest.md "The control channel"): one command line is written to
/// <c>&lt;dir&gt;/cmd</c> through a temporary file and a rename, and the game's reply is the line of <c>&lt;dir&gt;/out</c> that starts with the
/// command's id.
/// </summary>
sealed class ControlChannel(string directory)
{
    /// <summary>
    /// The control folder (absolute).
    /// </summary>
    public string Directory { get; } = Path.GetFullPath(directory);

    /// <summary>
    /// The last id used; ids only grow.
    /// </summary>
    long _lastId;

    /// <summary>
    /// Sends a command and returns its reply's <c>data</c>; throws <see cref="ChannelException"/> for an error reply or no reply in time.
    /// </summary>
    public JsonElement Send(string command, TimeSpan timeout)
    {
        long id = NextId();
        var deadline = Stopwatch.StartNew();
        WaitWhileCommandPending(deadline, timeout);
        string temp = Path.Combine(Directory, $".t{id}");
        File.WriteAllText(temp, $"{id} {command}\n");
        File.Move(temp, Path.Combine(Directory, "cmd"));
        return AwaitReply(id, command, deadline, timeout);
    }

    /// <summary>
    /// Whether the game answers a <c>state</c> within a few seconds.
    /// </summary>
    public bool Answers()
    {
        try
        {
            Send("state", TimeSpan.FromSeconds(3));
            return true;
        }
        catch (ChannelException)
        {
            return false;
        }
    }

    /// <summary>
    /// Empties the folder's command and reply files, for a game about to start on it.
    /// </summary>
    public void Clear()
    {
        System.IO.Directory.CreateDirectory(Directory);
        foreach (string name in new[] { "cmd", "out" })
            if (File.Exists(Path.Combine(Directory, name))) File.Delete(Path.Combine(Directory, name));
    }

    /// <summary>
    /// A fresh id: the clock in ticks, kept above the last one.
    /// </summary>
    long NextId()
    {
        _lastId = Math.Max(_lastId + 1, DateTime.UtcNow.Ticks);
        return _lastId;
    }

    /// <summary>
    /// Waits while the game has not yet taken the previous command (it deletes <c>cmd</c> once read).
    /// </summary>
    void WaitWhileCommandPending(Stopwatch elapsed, TimeSpan timeout)
    {
        while (File.Exists(Path.Combine(Directory, "cmd")))
        {
            if (elapsed.Elapsed > timeout) throw new ChannelException("the game did not take the previous command");
            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// Waits for the reply line of <paramref name="id"/> and returns its data.
    /// </summary>
    JsonElement AwaitReply(long id, string command, Stopwatch elapsed, TimeSpan timeout)
    {
        string prefix = $"{{\"id\":{id},";
        string outPath = Path.Combine(Directory, "out");
        while (elapsed.Elapsed <= timeout)
        {
            if (ReplyLine(outPath, prefix) is { } line) return Data(line, command);
            Thread.Sleep(100);
        }

        throw new ChannelException($"no reply to '{command}' within {timeout.TotalSeconds:0} s");
    }

    /// <summary>
    /// The reply line with the prefix, or null while there is none.
    /// </summary>
    static string? ReplyLine(string outPath, string prefix)
    {
        if (!File.Exists(outPath)) return null;

        using var stream = new FileStream(outPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
            if (line.StartsWith(prefix, StringComparison.Ordinal)) return line;

        return null;
    }

    /// <summary>
    /// A reply's data; throws for an error reply.
    /// </summary>
    static JsonElement Data(string line, string command)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (!root.GetProperty("ok").GetBoolean())
            throw new ChannelException($"'{command}': {(root.TryGetProperty("error", out var e) ? e.GetString() : "refused")}");

        return root.TryGetProperty("data", out var data) ? data.Clone() : default;
    }
}

/// <summary>
/// The control channel refused a command or did not answer.
/// </summary>
sealed class ChannelException(string message) : Exception(message);
