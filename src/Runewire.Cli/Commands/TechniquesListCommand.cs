using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Core.Infrastructure.Techniques;
using Runewire.Orchestrator.Techniques;

namespace Runewire.Cli.Commands;

/// <summary>
/// Lists the built-in injection techniques known to this Runewire build.
/// </summary>
public static class TechniquesListCommand
{
    public const string CommandName = "techniques";

    /// <summary>
    /// Creates the techniques command:
    ///   runewire techniques
    /// </summary>
    public static Command Create()
    {
        Command command = new(name: CommandName, description: "List available injection techniques.");

        command.SetAction(_ =>
        {
            return Handle();
        });

        return command;
    }

    private static int Handle()
    {
        // Use the built-in registry so the list matches what validation will accept.
        TechniqueCatalog catalog = new(new BuiltInInjectionTechniqueRegistry());

        IReadOnlyList<Domain.Techniques.InjectionTechniqueDescriptor> techniques = catalog.GetAll();

        CliConsole.WriteHeader("Supported injection techniques:", ConsoleColor.Cyan);

        foreach (Domain.Techniques.InjectionTechniqueDescriptor technique in techniques)
        {
            string kernel = technique.RequiresKernelMode ? "requires kernel mode" : "user-mode";
            CliConsole.WriteBullet($"{technique.Name} - {technique.DisplayName} [{technique.Category}] ({kernel})", ConsoleColor.Gray);
        }

        return 0;
    }
}
