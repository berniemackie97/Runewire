using System.CommandLine;
using Runewire.Cli.Commands;

namespace Runewire.Cli;

public static class Program
{
    /// <summary>
    /// Main entry method. Builds the root command, parses the command line,
    /// and asynchronously invokes the configured handler pipeline.
    ///
    /// That keeps parsing and invocation clearly separated and allows
    /// all handlers to be fully asynchronous.
    /// </summary>
    /// 
    /// <param name="args">Arguments passed from the command line.</param>
    /// 
    /// <returns>
    /// A task that completes with the process exit code produced by the invoked command.
    /// </returns>
    public static Task<int> Main(string[] args)
    {
        RootCommand rootCommand = BuildRootCommand();

        // Modern System.CommandLine pattern:
        //   1. Parse
        //   2. InvokeAsync on the ParseResult
        ParseResult parseResult = rootCommand.Parse(args);
        return parseResult.InvokeAsync();
    }

    /// <summary>
    /// Constructs the Runewire root command and registers all top level subcommands.
    /// </summary>
    /// 
    /// <returns>The configured <see cref="RootCommand"/> for the Runewire CLI.</returns>
    internal static RootCommand BuildRootCommand()
    {
        RootCommand root = new("Runewire process injection lab CLI")
        {
            // Core recipe commands:
            RecipeValidateCommand.Create(),
            RecipeRunCommand.Create(),
        };

        // Future extension points:
        // - Agent management (register/list/remove agents)
        // - Technique and injector introspection
        // - Run history / telemetry queries
        // - Lab configuration and policy inspection

        return root;
    }
}
