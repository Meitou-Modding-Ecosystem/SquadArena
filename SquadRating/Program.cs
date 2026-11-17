using SquadRating;

// SquadRating (docs/squad-rating.md): rates squads in Dust-bandit equivalents by fighting them in a running Meitou through the SquadArena mod.
var line = new CommandLine(args);
try
{
    return Commands.Run(line);
}
catch (UsageException e)
{
    Console.Error.WriteLine($"SquadRating {line.Command}: {e.Message}");
    return 2;
}
catch (ChannelException e)
{
    Console.Error.WriteLine($"SquadRating {line.Command}: the game's control channel: {e.Message}");
    return 3;
}
