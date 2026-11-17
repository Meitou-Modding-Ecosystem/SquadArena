using System.Text.Json;
using Meitou.Content;
using SquadArena;

namespace SquadRating;

/// <summary>
/// Where things are and how fights run (docs/squad-rating.md "Settings"): <c>squad-rating.json</c> in the working folder, then the command line's
/// flags over it.
/// </summary>
sealed record Settings
{
    /// <summary>
    /// The settings file, read from the working folder.
    /// </summary>
    public const string FileName = "squad-rating.json";

    /// <summary>
    /// The MeitouClient executable the tool starts.
    /// </summary>
    public string? GameExe { get; init; }

    /// <summary>
    /// The Kenshi folder; null finds it as the game does (KENSHI_PATH, meitou.local.json, Steam).
    /// </summary>
    public string? KenshiDir { get; init; }

    /// <summary>
    /// The arena save's name in Meitou's save folder.
    /// </summary>
    public string Save { get; init; } = "squad-arena";

    /// <summary>
    /// Start the game without a window.
    /// </summary>
    public bool Headless { get; init; }

    /// <summary>
    /// The control folder the started game listens on.
    /// </summary>
    public string ControlDir { get; init; } = Path.Combine(Path.GetTempPath(), "squad-rating-control");

    /// <summary>
    /// The arena's exchange folder; null: <c>&lt;Kenshi&gt;/mods/SquadArena/arena</c>.
    /// </summary>
    public string? ArenaDir { get; init; }

    /// <summary>
    /// Where fight results and the fight list are kept.
    /// </summary>
    public string RunsDir { get; init; } = "squad-rating-runs";

    /// <summary>
    /// Fights between reloads of the arena save (each at its own spot round the player).
    /// </summary>
    public int FightsPerLoad { get; init; } = 8;

    /// <summary>
    /// How far each fight spot is from the player's lead character.
    /// </summary>
    public float SpotDistance { get; init; } = 250;

    /// <summary>
    /// Game seconds before an undecided fight is a draw.
    /// </summary>
    public float TimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// The yardstick character: the one squads are measured in.
    /// </summary>
    public string Yardstick { get; init; } = "Dust Bandit";

    /// <summary>
    /// The settings file (when there is one) under the flags given.
    /// </summary>
    public static Settings Load(CommandLine line)
    {
        var settings = File.Exists(FileName)
            ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FileName), FightRequest.Json) ?? new Settings()
            : new Settings();
        return settings with
        {
            GameExe = line.Flag("game") ?? settings.GameExe,
            KenshiDir = line.Flag("kenshi") ?? settings.KenshiDir,
            Save = line.Flag("save") ?? settings.Save,
            Headless = line.Has("headless") || settings.Headless,
            ArenaDir = line.Flag("arena") ?? settings.ArenaDir,
            RunsDir = line.Flag("runs") ?? settings.RunsDir,
        };
    }

    /// <summary>
    /// The game install.
    /// </summary>
    public GameInstall Install() =>
        (KenshiDir is { } dir ? GameInstall.Open(dir) : GameInstall.Locate())
            ?? throw new UsageException("no Kenshi install found: set KENSHI_PATH, or \"kenshiDir\" in squad-rating.json, or pass --kenshi");

    /// <summary>
    /// The arena's exchange folder.
    /// </summary>
    public ArenaFolders Arena() => new(ArenaDir ?? Path.Combine(Install().ModsDirectory, "SquadArena", "arena"));

    /// <summary>
    /// A save's folder in Meitou's own save folder.
    /// </summary>
    public static string SaveFolder(string name) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Meitou", "save", name);
}
