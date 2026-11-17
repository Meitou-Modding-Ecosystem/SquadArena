using Meitou.Data;
using Meitou.Data.Fcs;
using SquadArena;

namespace SquadRating;

/// <summary>
/// The tool's commands (docs/squad-rating.md "Commands"). The ones that fight start the game on the arena save (or attach to one with
/// <c>--attach &lt;control folder&gt;</c>), run their fights, quit the game they started and print the ratings of the squads they touched.
/// </summary>
static class Commands
{
    /// <summary>
    /// What <c>help</c> prints.
    /// </summary>
    const string Usage = """
        SquadRating <command> [options]          (full guide: docs/squad-rating.md)

          find <text> [--kind character|template|race|item|maker|model]   records whose name contains the text
          check <squad file>                     resolve a squad file's names and show its roster
          capture --save <save> --squad <name> [--out <file>]   write a squad file from one of your squads in a save
          rate <squad file>... [--fights 12]     fight each squad against the yardstick and rate it
          compare <squad A> <squad B> [--fights 10]   fight two squads head to head
          bench [--filter <text>] [--fights 8] [--fresh]   rate every town guard and patrol template at squad size 1
          report [--csv <file>]                  the ratings from every fight run in this folder
          write-mod <file>                       write SquadArena.mod (the two arena factions)

        Options for commands that fight: --game <MeitouClient exe> --save <arena save> --headless --attach <control folder>
        Settings file: squad-rating.json in the working folder (docs/squad-rating.md "Settings").
        """;

    /// <summary>
    /// Runs the command; the exit code.
    /// </summary>
    public static int Run(CommandLine line)
    {
        switch (line.Command)
        {
            case "find": Find(line); break;
            case "check": Check(line); break;
            case "capture": Capture(line); break;
            case "rate": Rate(line); break;
            case "compare": Compare(line); break;
            case "bench": Bench(line); break;
            case "report": Report(line, null); break;
            case "write-mod": WriteMod(line); break;
            case "" or "help" or "--help": Console.WriteLine(Usage); break;
            default: throw new UsageException($"unknown command; run SquadRating help");
        }

        return 0;
    }

    #region Squad files

    /// <summary>
    /// <c>find</c>: lists matching records.
    /// </summary>
    static void Find(CommandLine line)
    {
        string text = line.Word(0, "the text to look for");
        string[] kinds = line.Flag("kind") is { } kind ? [KindOf(kind)] : [.. GameLookup.Kinds.Keys];
        var hits = Lookup(Settings.Load(line)).Find(text, kinds);
        foreach (var r in hits.Take(line.Int("limit", 40))) Console.WriteLine(GameLookup.Line(r));
        if (hits.Count == 0) Console.WriteLine("nothing found");
    }

    /// <summary>
    /// <c>check</c>: resolves a squad file and prints its roster with ids.
    /// </summary>
    static void Check(CommandLine line)
    {
        var game = Lookup(Settings.Load(line));
        var side = Resolve(line.Word(0, "a squad file"), game);
        Console.WriteLine($"{side.Name}: {SquadFile.KeyOf(side)}");
        foreach (var m in side.Members) Console.WriteLine("  " + Describe(m, game.Db));
    }

    /// <summary>
    /// <c>capture</c>: writes a squad file from a save.
    /// </summary>
    static void Capture(CommandLine line)
    {
        var settings = Settings.Load(line);
        string save = line.Flag("save") ?? throw new UsageException("capture needs --save <save name>");
        string squad = line.Flag("squad") ?? throw new UsageException("capture needs --squad <squad name>");
        var file = SquadCapture.Capture(settings.Install(), Lookup(settings).Db, save, squad);
        string path = line.Flag("out") ?? $"{string.Concat(file.Name.Split(Path.GetInvalidFileNameChars()))}.squad.json";
        file.Save(path);
        Console.WriteLine($"Wrote {path}: {file.Members.Length} members of '{file.Name}'");
    }

    #endregion

    #region Fights

    /// <summary>
    /// <c>rate</c>: a staircase against the yardstick for each squad file.
    /// </summary>
    static void Rate(CommandLine line)
    {
        var settings = Settings.Load(line);
        var game = Lookup(settings);
        var squads = line.Words.Select(path => Contender(path, game)).ToList();
        if (squads.Count == 0) throw new UsageException("rate needs a squad file");

        Fight(line, settings, game, (runner, ladder) =>
        {
            foreach (var squad in squads)
            {
                Console.WriteLine($"Rating {squad.Key.Name}");
                ladder.Climb(squad, line.Int("fights", 12), Ladder.StartOf(squad.Side));
            }
        });
        Report(line, [.. squads.Select(s => s.Key.Key)]);
    }

    /// <summary>
    /// <c>compare</c>: two squads head to head, sides swapped every fight.
    /// </summary>
    static void Compare(CommandLine line)
    {
        var settings = Settings.Load(line);
        var game = Lookup(settings);
        var a = Contender(line.Word(0, "two squad files"), game);
        var b = Contender(line.Word(1, "two squad files"), game);
        int fights = line.Int("fights", 10);
        double score = 0;
        Fight(line, settings, game, (runner, _) =>
        {
            for (int k = 0; k < fights; k++)
                score += k % 2 == 0 ? runner.Fight(a, b).Score(0) : runner.Fight(b, a).Score(1);
        });

        Console.WriteLine($"{a.Key.Name} scored {score:0.#} of {fights} against {b.Key.Name} (a draw counts half)");
        Report(line, [a.Key.Key, b.Key.Key]);
    }

    /// <summary>
    /// <c>bench</c>: a staircase for every guard and patrol template; a template with enough fights on the list already is skipped unless
    /// <c>--fresh</c>.
    /// </summary>
    static void Bench(CommandLine line)
    {
        var settings = Settings.Load(line);
        var game = Lookup(settings);
        int fights = line.Int("fights", 8);
        var history = new FightHistory(settings.RunsDir).All();
        var entries = BenchList.Build(game.Db)
            .Where(e => line.Flag("filter") is not { } f || e.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var todo = entries.Where(e => line.Has("fresh") || history.Count(f => f.Sides.Any(s => s.Key == e.Contender.Key.Key)) < fights).ToList();
        Console.WriteLine($"Bench: {entries.Count} templates, {todo.Count} to fight ({fights} fights each)");

        Fight(line, settings, game, (_, ladder) =>
        {
            foreach (var entry in todo)
            {
                Console.WriteLine($"Rating {entry.Name} ({entry.Role})");
                ladder.Climb(entry.Contender, fights, 3);
            }
        });
        Report(line, [.. entries.Select(e => e.Contender.Key.Key)]);
    }

    /// <summary>
    /// Opens the game (started or attached), runs <paramref name="work"/> and closes a game it started.
    /// </summary>
    static void Fight(CommandLine line, Settings settings, GameLookup game, Action<FightRunner, Ladder> work)
    {
        var (yardstick, yardstickName) = YardstickOf(settings, game);
        using var session = line.Flag("attach") is { } dir ? GameSession.Attach(settings, dir) : GameSession.Start(settings);
        var runner = new FightRunner(session, settings.Arena(), new FightHistory(settings.RunsDir), settings);
        work(runner, new Ladder(runner, yardstick, yardstickName));
    }

    #endregion

    #region Ratings

    /// <summary>
    /// <c>report</c>, and the table after a fighting command: the fitted ratings of <paramref name="keys"/> (every squad when null).
    /// </summary>
    static void Report(CommandLine line, IReadOnlyCollection<string>? keys)
    {
        var settings = Settings.Load(line);
        var fights = new FightHistory(settings.RunsDir).All();
        if (fights.Count == 0) throw new UsageException($"no fights in {settings.RunsDir} yet");

        var (ratings, beta) = RatingModel.Fit(fights);
        var shown = ratings.Where(r => keys is null || keys.Contains(r.Key)).ToList();
        Console.WriteLine($"\nRatings in {settings.Yardstick} equivalents ({fights.Count} fights, β {beta:0.00})");
        Console.WriteLine($"{"Squad",-40} {"Rating",7}  {"90% range",-13} {"Fights",6} {"Score",6}");
        foreach (var r in shown) Console.WriteLine($"{Clip(r.Name, 40),-40} {r.Rating,7:0.0}  {r.Low,5:0.0} - {r.High,-5:0.0} {r.Fights,6} {r.Score,6:0.#}");
        if (line.Flag("csv") is { } csv) WriteCsv(csv, shown);
    }

    /// <summary>
    /// Writes ratings as CSV.
    /// </summary>
    static void WriteCsv(string path, IEnumerable<RatedSquad> ratings)
    {
        var lines = ratings.Select(r => string.Join(',', Quote(r.Name), r.Key, $"{r.Rating:0.00}", $"{r.Low:0.00}", $"{r.High:0.00}", r.Fights, r.Score));
        File.WriteAllLines(path, ["name,key,rating,low90,high90,fights,score", .. lines]);
        Console.WriteLine($"Wrote {path}");
    }

    #endregion

    /// <summary>
    /// <c>write-mod</c>: SquadArena.mod.
    /// </summary>
    static void WriteMod(CommandLine line)
    {
        string path = line.Word(0, "the file to write");
        FcsWriter.WriteFile(ArenaData.Build(), path);
        Console.WriteLine($"Wrote {path}");
    }

    #region Helpers

    /// <summary>
    /// The merged game data of the install and its mod list.
    /// </summary>
    static GameLookup Lookup(Settings settings) => new(GameDatabase.Load(LoadOrder.FromInstall(settings.Install())));

    /// <summary>
    /// A squad file resolved; a usage error listing every problem.
    /// </summary>
    static SideRequest Resolve(string path, GameLookup game)
    {
        var (side, errors) = SquadFile.Load(path).Resolve(game);
        return side ?? throw new UsageException($"{path}:\n  {string.Join("\n  ", errors)}");
    }

    /// <summary>
    /// A squad file as a contender.
    /// </summary>
    static Contender Contender(string path, GameLookup game)
    {
        var side = Resolve(path, game);
        return new Contender(new SideKey(SquadFile.KeyOf(side), side.Name), side);
    }

    /// <summary>
    /// The yardstick character's id and name.
    /// </summary>
    static (string Id, string Name) YardstickOf(Settings settings, GameLookup game)
    {
        var (id, error) = game.Resolve(settings.Yardstick, "character");
        return id is null ? throw new UsageException($"the yardstick: {error}") : (id, game.Db.Find(id)!.Name);
    }

    /// <summary>
    /// A <c>--kind</c> value checked.
    /// </summary>
    static string KindOf(string kind) =>
        GameLookup.Kinds.ContainsKey(kind) ? kind : throw new UsageException($"--kind is one of {string.Join(", ", GameLookup.Kinds.Keys)}");

    /// <summary>
    /// A resolved member as a line: count, record names and the overrides.
    /// </summary>
    static string Describe(SideMember m, GameDatabase db)
    {
        string Name(string? id) => id is null ? "" : db.Find(id)?.Name ?? id;
        string who = m.Template is { } t ? $"template {Name(t)}" : Name(m.Character);
        string race = m.Race is { } r ? $", race {Name(r)}" : "";
        string gear = m.Gear is { } g ? $", gear: {string.Join(", ", g.Select(i => Name(i.Item)))}" : "";
        string stats = m.Stats is { Count: > 0 } s ? $", {s.Count} stats set" : "";
        return $"{m.Count} x {who}{race}{gear}{stats}";
    }

    /// <summary>
    /// Text cut to a width.
    /// </summary>
    static string Clip(string text, int width) => text.Length <= width ? text : text[..(width - 1)] + "…";

    /// <summary>
    /// A CSV field, quoted.
    /// </summary>
    static string Quote(string text) => $"\"{text.Replace("\"", "\"\"")}\"";

    #endregion
}
