using System.CommandLine;
using Runewire.Cli.Commands;

namespace Runewire.Cli;

public static class Program
{
    /// <summary>
    /// CLI entry point.
    /// Keep this boring. Build commands, parse args, invoke.
    /// Returning Task<int> keeps everything async without weird sync blocking.
    /// </summary>
    /// <param name="args">Raw command line args.</param>
    /// <returns>The exit code from whatever command ran.</returns>
    public static Task<int> Main(string[] args)
    {
        RootCommand rootCommand = BuildRootCommand();

        ParseResult parseResult = rootCommand.Parse(args);
        return parseResult.InvokeAsync();
    }

    /// <summary>
    /// Builds the root command and registers the top level commands.
    /// Add new commands here.
    /// </summary>
    internal static RootCommand BuildRootCommand()
    {
        RootCommand root = new("Runewire process injection lab CLI")
        {
            // Core recipe commands. This is the main workflow right now.
            RecipeValidateCommand.Create(),
            RecipeRunCommand.Create(),
        };

        // Stuff I'll will probably add later:
        // agent management
        // technique/injector introspection
        // run history / telemetry queries
        // lab config and policy inspection

        return root;
    }
}
