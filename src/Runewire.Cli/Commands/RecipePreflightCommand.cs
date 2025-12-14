using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Core.Infrastructure.Recipes;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;
using Runewire.Orchestrator.Infrastructure.Preflight;
using Runewire.Orchestrator.Infrastructure.Services;
using System.Text.Json;

namespace Runewire.Cli.Commands;

/// <summary>
/// Runs validation + preflight checks without executing an engine.
/// </summary>
public static class RecipePreflightCommand
{
    public const string CommandName = "preflight";

    private const int ExitCodeSuccess = 0;
    private const int ExitCodeValidationError = 1;
    private const int ExitCodeLoadOrOtherError = 2;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    public static Command Create()
    {
        Argument<FileInfo> recipeArgument = new("recipe")
        {
            Description = "Path to the recipe file (YAML or JSON) to preflight.",
        };

        Option<bool> jsonOption = new("--json")
        {
            Description = "Emit machine-readable JSON output instead of human-readable text.",
        };

        Command command = new(name: CommandName, description: "Validate and preflight a Runewire recipe without executing it.")
        {
            recipeArgument,
            jsonOption,
        };

        command.SetAction(parseResult =>
        {
            FileInfo? recipeFile = parseResult.GetValue(recipeArgument);
            bool outputJson = parseResult.GetValue(jsonOption);

            if (recipeFile is null)
            {
                if (outputJson)
                {
                    WriteJson(new { status = "error", message = "No recipe file specified." });
                }
                else
                {
                    CliConsole.WriteError("No recipe file specified.");
                }
                return ExitCodeLoadOrOtherError;
            }

            return Handle(recipeFile, outputJson);
        });

        return command;
    }

    private static int Handle(FileInfo recipeFile, bool outputJson)
    {
        if (!recipeFile.Exists)
        {
            if (outputJson)
            {
                WriteJson(new { status = "error", message = $"Recipe file not found: {recipeFile.FullName}" });
            }
            else
            {
                CliConsole.WriteError($"Recipe file not found: {recipeFile.FullName}");
            }
            return ExitCodeLoadOrOtherError;
        }

        RecipeExecutionService service = new(new DefaultRecipeLoaderProvider(), new ProcessTargetPreflightChecker(), new PayloadPreflightChecker(), new InjectionEngineFactory());

        try
        {
            RecipeValidationOutcome outcome = service.Validate(recipeFile.FullName);

            if (outputJson)
            {
                WriteJson(new
                {
                    status = "valid",
                    recipeName = outcome.Recipe.Name,
                    meta = BuildMeta(),
                    preflight = outcome.Preflight
                });
            }
            else
            {
                CliConsole.WriteSuccess($"Recipe is valid and passed preflight: {outcome.Recipe.Name}");
                CliConsole.WriteDetail($"Target preflight: {(outcome.Preflight.TargetSuccess ? "ok" : "failed")}");
                CliConsole.WriteDetail($"Payload preflight: {(outcome.Preflight.PayloadSuccess ? "ok" : "failed")}");
            }

            return ExitCodeSuccess;
        }
        catch (RecipeLoadException ex)
        {
            if (ex.ValidationErrors.Count > 0)
            {
                if (outputJson)
                {
                    WriteJson(new
                    {
                        status = "invalid",
                        meta = BuildMeta(),
                        errors = ex.ValidationErrors.Select(e => new { code = e.Code, message = e.Message }).ToArray()
                    });
                }
                else
                {
                    CliConsole.WriteHeader("Recipe is invalid.", ConsoleColor.Yellow);
                    foreach (RecipeValidationError error in ex.ValidationErrors)
                    {
                        CliConsole.WriteBullet($"[{error.Code}] {error.Message}", ConsoleColor.Yellow);
                    }
                }

                return ExitCodeValidationError;
            }

            if (outputJson)
            {
                WriteJson(new { status = "error", meta = BuildMeta(), message = ex.Message, inner = ex.InnerException?.Message });
            }
            else
            {
                CliConsole.WriteHeader("Failed to load recipe.", ConsoleColor.Red);
                CliConsole.WriteError(ex.Message);

                if (ex.InnerException is not null)
                {
                    CliConsole.WriteDetail($"Inner: {ex.InnerException.Message}");
                }
            }

            return ExitCodeLoadOrOtherError;
        }
        catch (Exception ex)
        {
            if (outputJson)
            {
                WriteJson(new { status = "error", meta = BuildMeta(), message = ex.Message });
            }
            else
            {
                CliConsole.WriteHeader("Unexpected error while preflighting recipe.", ConsoleColor.Red);
                CliConsole.WriteError(ex.Message);
            }
            return ExitCodeLoadOrOtherError;
        }
    }

    private static void WriteJson(object payload)
    {
        string json = JsonSerializer.Serialize(payload, s_jsonOptions);
        Console.WriteLine(json);
    }

    private static object BuildMeta()
    {
        Version? version = typeof(Program).Assembly.GetName().Version;
        return new { version = version?.ToString() ?? "unknown" };
    }
}
