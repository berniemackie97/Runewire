using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Core.Infrastructure.Recipes;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;
using Runewire.Orchestrator.Infrastructure.Preflight;
using Runewire.Orchestrator.Infrastructure.Services;
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

    /// <summary>
    /// Main validate logic. Returns an exit code (see constants above).
    /// </summary>
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

        RecipeExecutionService service = new(new DefaultRecipeLoaderProvider(), new ProcessTargetPreflightChecker(), new InjectionEngineFactory());

        try
        {
            RunewireRecipe recipe = service.Validate(recipeFile.FullName);

            if (outputJson)
            {
                WriteJson(new { status = "valid", recipeName = recipe.Name });
            }
            else
            {
                CliConsole.WriteSuccess($"Recipe is valid: {recipe.Name}");
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
                    WriteJson(new
                    {
                        status = "invalid",
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

            // Parse/IO/structural failure.
            if (outputJson)
            {
                WriteJson(new { status = "error", message = ex.Message, inner = ex.InnerException?.Message });
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
                WriteJson(new { status = "error", message = ex.Message });
            }
            else
            {
                CliConsole.WriteHeader("Unexpected error while validating recipe.", ConsoleColor.Red);
                CliConsole.WriteError(ex.Message);
            }
            return ExitCodeLoadOrOtherError;
        }
    }

    private static void ThrowValidation(IEnumerable<RecipeValidationError> errors)
    {
        List<RecipeValidationError> list = errors?.ToList() ?? [];
        throw new RecipeLoadException("Recipe failed preflight.", list);
    }

    private static void WriteJson(object payload)
    {
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
}
