using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Cli.Infrastructure.Output;
using Runewire.Core.Infrastructure.Techniques;
using Runewire.Orchestrator.Techniques;
using System.Text.Json;

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
        Option<bool> jsonOption = new("--json")
        {
            Description = "Emit machine-readable JSON output instead of human-readable text.",
        };

        Command command = new(name: CommandName, description: "List available injection techniques.")
        {
            jsonOption
        };

        command.SetAction(parseResult =>
        {
            bool outputJson = parseResult.GetValue(jsonOption);
            return Handle(outputJson);
        });

        return command;
    }

    private static int Handle(bool outputJson)
    {
        // Use the built-in registry so the list matches what validation will accept.
        TechniqueCatalog catalog = new(new BuiltInInjectionTechniqueRegistry());

        IReadOnlyList<Domain.Techniques.InjectionTechniqueDescriptor> techniques = catalog.GetAll();

        if (outputJson)
        {
            WriteJson(JsonResponseFactory.TechniqueList(techniques));
            return 0;
        }

        CliConsole.WriteHeader("Supported injection techniques:", ConsoleColor.Cyan);

        foreach (Domain.Techniques.InjectionTechniqueDescriptor technique in techniques)
        {
            string kernel = technique.RequiresKernelMode ? "requires kernel mode" : "user-mode";
            string platforms = string.Join(", ", technique.Platforms);
            string requiredParams = technique.RequiredParameters.Count > 0
                ? $"params: {string.Join(", ", technique.RequiredParameters)}"
                : "params: none";
            CliConsole.WriteBullet($"{technique.Name} - {technique.DisplayName} [{technique.Category}] ({kernel}; platforms: {platforms}; {requiredParams})", ConsoleColor.Gray);
        }

        return 0;
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static void WriteJson(object payload)
    {
        string json = JsonSerializer.Serialize(payload, s_jsonOptions);
        Console.WriteLine(json);
    }
}
