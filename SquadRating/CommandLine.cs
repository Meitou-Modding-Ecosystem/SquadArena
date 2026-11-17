namespace SquadRating;

/// <summary>
/// A command line: the command, its plain words and its <c>--flag value</c> or <c>--switch</c> options.
/// </summary>
sealed class CommandLine
{
    /// <summary>
    /// Options that take no value.
    /// </summary>
    static readonly HashSet<string> Switches = ["headless", "all", "fresh"];

    /// <summary>
    /// The options given, by name without the dashes.
    /// </summary>
    readonly Dictionary<string, string?> _flags = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The command (the first word), lower case; empty when none was given.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// The words after the command that are not options.
    /// </summary>
    public List<string> Words { get; } = [];

    /// <summary>
    /// Parses <paramref name="args"/>.
    /// </summary>
    public CommandLine(IReadOnlyList<string> args)
    {
        Command = args.Count > 0 ? args[0].ToLowerInvariant() : "";
        for (int i = 1; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                Words.Add(args[i]);
                continue;
            }

            string name = args[i][2..];
            bool takesValue = !Switches.Contains(name) && i + 1 < args.Count;
            _flags[name] = takesValue ? args[++i] : null;
        }
    }

    /// <summary>
    /// An option's value; null when it was not given.
    /// </summary>
    public string? Flag(string name) => _flags.GetValueOrDefault(name);

    /// <summary>
    /// Whether an option was given.
    /// </summary>
    public bool Has(string name) => _flags.ContainsKey(name);

    /// <summary>
    /// A whole-number option, else <paramref name="fallback"/>.
    /// </summary>
    public int Int(string name, int fallback)
    {
        if (Flag(name) is not { } text) return fallback;

        return int.TryParse(text, out int n) && n > 0 ? n : throw new UsageException($"--{name} needs a whole number above 0");
    }

    /// <summary>
    /// The plain word at <paramref name="index"/>; a usage error naming <paramref name="what"/> when it is missing.
    /// </summary>
    public string Word(int index, string what) => index < Words.Count ? Words[index] : throw new UsageException($"{Command} needs {what}");
}

/// <summary>
/// A mistake in how the tool was called or set up; printed without a stack trace.
/// </summary>
sealed class UsageException(string message) : Exception(message);
