using System.Diagnostics;
using System.Text.Json;

namespace SquadRating;

/// <summary>
/// The running Meitou the fights happen in (docs/squad-rating.md "Running fights"): started by the tool on the arena save with the control channel,
/// or an already running one with <c>--attach</c>. Between batches of fights the arena save is loaded again, so bodies don't pile up.
/// </summary>
sealed class GameSession : IDisposable
{
    /// <summary>
    /// How long a load may take (a save loads in about 20 s; a slow disk takes longer).
    /// </summary>
    static readonly TimeSpan LoadTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The channel to the game.
    /// </summary>
    public ControlChannel Channel { get; }

    /// <summary>
    /// The arena save's name in Meitou's save folder.
    /// </summary>
    readonly string _save;

    /// <summary>
    /// The game this tool started; null when attached.
    /// </summary>
    readonly Process? _process;

    /// <summary>
    /// A session on <paramref name="channel"/>, the game already running (attached) or started as <paramref name="process"/>.
    /// </summary>
    GameSession(ControlChannel channel, string save, Process? process)
    {
        Channel = channel;
        _save = save;
        _process = process;
    }

    /// <summary>
    /// Starts the game on the arena save with a fresh control folder and waits for the world.
    /// </summary>
    public static GameSession Start(Settings settings)
    {
        string exe = settings.GameExe ?? throw new UsageException("set \"game\" in squad-rating.json (the MeitouClient executable) or pass --game");
        string saveFolder = Settings.SaveFolder(settings.Save);
        if (!Directory.Exists(saveFolder)) throw new UsageException($"no save '{settings.Save}' in {Path.GetDirectoryName(saveFolder)}");

        var channel = new ControlChannel(settings.ControlDir);
        channel.Clear();
        var start = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exe))!, UseShellExecute = false };
        foreach (string a in new[] { "--control-dir", channel.Directory, "--load", saveFolder, settings.Headless ? "--headless" : "--windowed" })
            start.ArgumentList.Add(a);

        Console.WriteLine($"Starting {exe} on '{settings.Save}'{(settings.Headless ? " (headless)" : "")}");
        var session = new GameSession(channel, settings.Save, Process.Start(start));
        session.WaitForWorld();
        return session;
    }

    /// <summary>
    /// Attaches to a game already running with <c>--control-dir</c> on <paramref name="controlDir"/>.
    /// </summary>
    public static GameSession Attach(Settings settings, string controlDir)
    {
        var channel = new ControlChannel(controlDir);
        if (!channel.Answers()) throw new UsageException($"no game answers on {channel.Directory}; start Meitou with --control-dir \"{channel.Directory}\"");
        return new GameSession(channel, settings.Save, null);
    }

    /// <summary>
    /// Loads the arena save again and runs it at the fastest speed.
    /// </summary>
    public void Reload()
    {
        Channel.Send($"quickload {_save}", TimeSpan.FromSeconds(10));
        Thread.Sleep(2000);
        WaitForWorld();
    }

    /// <summary>
    /// Runs the game at speed 5 (a running game, so fights don't stop and start the way stepping a paused game does).
    /// </summary>
    public void Run() => Channel.Send("speed 5", TimeSpan.FromSeconds(5));

    /// <summary>
    /// The player's lead character's position (x, z), the centre the fight spots are laid out round.
    /// </summary>
    public (float X, float Z) LeadPosition()
    {
        var brief = Channel.Send("observe --brief", TimeSpan.FromSeconds(5));
        if (!brief.TryGetProperty("pos", out var pos) || pos.GetArrayLength() < 2)
            throw new UsageException("the arena save has no player character to place the fights round");

        return (pos[0].GetSingle(), pos[1].GetSingle());
    }

    /// <summary>
    /// Waits until the game shows a loaded world.
    /// </summary>
    void WaitForWorld()
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < LoadTimeout)
        {
            if (_process is { HasExited: true }) throw new UsageException($"the game exited (code {_process.ExitCode}); see its log.txt");
            if (Screen() == "world") return;
            Thread.Sleep(1000);
        }

        throw new UsageException($"the game did not show the world within {LoadTimeout.TotalMinutes:0} minutes");
    }

    /// <summary>
    /// The screen the game shows, or null while it does not answer.
    /// </summary>
    string? Screen()
    {
        try
        {
            var state = Channel.Send("state", TimeSpan.FromSeconds(3));
            return state.ValueKind == JsonValueKind.Object && state.TryGetProperty("screen", out var s) ? s.GetString() : null;
        }
        catch (ChannelException)
        {
            return null;
        }
    }

    /// <summary>
    /// Quits a game this tool started.
    /// </summary>
    public void Dispose()
    {
        if (_process is null || _process.HasExited) return;

        try
        {
            Channel.Send("quit", TimeSpan.FromSeconds(5));
        }
        catch (ChannelException)
        {
            _process.Kill();
        }

        _process.Dispose();
    }
}
