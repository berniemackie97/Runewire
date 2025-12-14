using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Cli.Infrastructure.Output;
using Runewire.Core.Infrastructure.Recipes;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Infrastructure.Services;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;
using Runewire.Orchestrator.Infrastructure.Preflight;
using System.Text.Json;

namespace Runewire.Cli.Commands;

/// <summary>
/// Validates a recipe YAML file.
/// This does not run anything. It just loads + checks the recipe and prints errors.
/// </summary>
public static class RecipeValidateCommand
{
    public const string CommandName = "validate";

    // Exit codes for validate:
    // 0 = valid
    // 1 = recipe loaded but has validation errors
    // 2 = file missing, parse/load error, IO, or unexpected error
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeValidationError = 1;
    private const int ExitCodeLoadOrOtherError = 2;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates the command:
    /// runewire validate recipe.yaml
    /// </summary>
    public static Command Create()
    {
        Argument<FileInfo> recipeArgument = new("recipe")
        {
            Description = "Path to the recipe file (YAML or JSON) to validate.",
        };

        Option<bool> jsonOption = new("--json")
        {
            Description = "Emit machine-readable JSON output instead of human-readable text.",
        };

        Command command = new(name: CommandName, description: "Validate a Runewire recipe file (YAML or JSON).")
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
                    WriteJson(JsonResponseFactory.ValidationError("No recipe file specified.", null));
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

    /// <summary>
    /// Main validate logic. Returns an exit code (see constants above).
    /// </summary>
    private static int Handle(FileInfo recipeFile, bool outputJson)
    {
        if (!recipeFile.Exists)
        {
            if (outputJson)
            {
                WriteJson(JsonResponseFactory.ValidationError($"Recipe file not found: {recipeFile.FullName}", null));
            }
            else
            {
                CliConsole.WriteError($"Recipe file not found: {recipeFile.FullName}");
            }
            return ExitCodeLoadOrOtherError;
        }

        RecipeExecutionService service = new(new DefaultRecipeLoaderProvider(), new ProcessTargetPreflightChecker(), new PayloadPreflightChecker(), new InjectionEngineFactory(), new NativeVersionPreflightChecker(new FileNativeVersionProvider(), new Core.Infrastructure.Techniques.BuiltInInjectionTechniqueRegistry()));

        try
        {
            RecipeValidationOutcome outcome = service.Validate(recipeFile.FullName);

            if (outputJson)
            {
                WriteJson(JsonResponseFactory.ValidationSuccess(outcome));
            }
            else
            {
                CliConsole.WriteSuccess($"Recipe is valid: {outcome.Recipe.Name}");
            }
            return ExitCodeSuccess;
        }
        catch (RecipeLoadException ex)
        {
            // Loaded enough to produce semantic validation errors.
            if (ex.ValidationErrors.Count > 0)
            {
                if (outputJson)
                {
                    WriteJson(JsonResponseFactory.ValidationInvalid(ex.ValidationErrors));
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

            // Parse/IO/structural failure.
            if (outputJson)
            {
                WriteJson(JsonResponseFactory.ValidationError(ex.Message, ex.InnerException));
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
            // Just fail clean.
            if (outputJson)
            {
                WriteJson(JsonResponseFactory.ValidationError(ex.Message, null));
            }
            else
            {
                CliConsole.WriteHeader("Unexpected error while validating recipe.", ConsoleColor.Red);
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

}
