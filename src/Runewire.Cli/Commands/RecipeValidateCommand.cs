using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using Runewire.Core.Infrastructure.Recipes;
using Runewire.Core.Infrastructure.Validation;

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
            Description = "Path to the recipe YAML file to validate.",
        };

        Command command = new(name: CommandName, description: "Validate a Runewire recipe YAML file.")
        {
            recipeArgument,
        };

        command.SetAction(parseResult =>
        {
            FileInfo? recipeFile = parseResult.GetValue(recipeArgument);

            if (recipeFile is null)
            {
                CliConsole.WriteError("No recipe file specified.");
                return ExitCodeLoadOrOtherError;
            }

            return Handle(recipeFile);
        });

        return command;
    }

    /// <summary>
    /// Main validate logic. Returns an exit code (see constants above).
    /// </summary>
    private static int Handle(FileInfo recipeFile)
    {
        if (!recipeFile.Exists)
        {
            CliConsole.WriteError($"Recipe file not found: {recipeFile.FullName}");
            return ExitCodeLoadOrOtherError;
        }

        // Use the shared factory so CLI and other entry points all validate the same way.
        // Saves me from chasing dumb mismatches where one accepts a recipe and the other rejects it.
        BasicRecipeValidator validator = RecipeValidatorFactory.CreateDefaultValidator();
        IRecipeLoader loader = RecipeLoaderSelector.CreateForPath(recipeFile.FullName, validator);

        try
        {
            RunewireRecipe recipe = loader.LoadFromFile(recipeFile.FullName);

            CliConsole.WriteSuccess($"Recipe is valid: {recipe.Name}");
            return ExitCodeSuccess;
        }
        catch (RecipeLoadException ex)
        {
            // Loaded enough to produce semantic validation errors.
            if (ex.ValidationErrors.Count > 0)
            {
                CliConsole.WriteHeader("Recipe is invalid.", ConsoleColor.Yellow);
                foreach (RecipeValidationError error in ex.ValidationErrors)
                {
                    CliConsole.WriteBullet($"[{error.Code}] {error.Message}", ConsoleColor.Yellow);
                }

                return ExitCodeValidationError;
            }

            // Parse/IO/structural failure.
            CliConsole.WriteHeader("Failed to load recipe.", ConsoleColor.Red);
            CliConsole.WriteError(ex.Message);

            if (ex.InnerException is not null)
            {
                CliConsole.WriteDetail($"Inner: {ex.InnerException.Message}");
            }

            return ExitCodeLoadOrOtherError;
        }
        catch (Exception ex)
        {
            // Just fail clean.
            CliConsole.WriteHeader("Unexpected error while validating recipe.", ConsoleColor.Red);
            CliConsole.WriteError(ex.Message);
            return ExitCodeLoadOrOtherError;
        }
    }
}
