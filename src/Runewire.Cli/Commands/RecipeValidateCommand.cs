using System.CommandLine;
using Runewire.Core.Domain.Recipes;
using Runewire.Core.Domain.Techniques;
using Runewire.Core.Domain.Validation;
using Runewire.Core.Infrastructure.Recipes;

namespace Runewire.Cli.Commands;

/// <summary>
/// CLI command for validating recipe YAML files.
/// </summary>
public static class RecipeValidateCommand
{
    public const string CommandName = "validate";

    // Exit codes for the 'validate' command.
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeValidationError = 1;
    private const int ExitCodeLoadOrOtherError = 2;

    // Technique registry is immutable and safe to reuse across invocations.
    private static readonly BuiltInInjectionTechniqueRegistry TechniqueRegistry = new();

    /// <summary>
    /// Creates the 'validate' command:
    ///   runewire validate &lt;recipe.yaml&gt;
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
                WriteError("No recipe file specified.");
                return ExitCodeLoadOrOtherError;
            }

            return Handle(recipeFile);
        });

        return command;
    }

    /// <summary>
    /// Core handler logic for validation. Returns an exit code:
    /// 0 = valid
    /// 1 = validation errors (semantic)
    /// 2 = load/structural/other error
    /// </summary>
    private static int Handle(FileInfo recipeFile)
    {
        if (!recipeFile.Exists)
        {
            WriteError($"Recipe file not found: {recipeFile.FullName}");
            return ExitCodeLoadOrOtherError;
        }

        BasicRecipeValidator validator = CreateValidator();
        YamlRecipeLoader loader = new(validator);

        try
        {
            RunewireRecipe recipe = loader.LoadFromFile(recipeFile.FullName);

            WriteSuccess($"Recipe is valid: {recipe.Name}");
            return ExitCodeSuccess;
        }
        catch (RecipeLoadException ex)
        {
            if (ex.ValidationErrors.Count > 0)
            {
                WriteHeader("Recipe is invalid.", ConsoleColor.Yellow);
                foreach (RecipeValidationError error in ex.ValidationErrors)
                {
                    WriteBullet($"[{error.Code}] {error.Message}", ConsoleColor.Yellow);
                }

                return ExitCodeValidationError;
            }

            // Structural / I/O / parse error.
            WriteHeader("Failed to load recipe.", ConsoleColor.Red);
            WriteError(ex.Message);

            if (ex.InnerException is not null)
            {
                WriteDetail($"Inner: {ex.InnerException.Message}");
            }

            return ExitCodeLoadOrOtherError;
        }
        catch (Exception ex)
        {
            WriteHeader("Unexpected error while validating recipe.", ConsoleColor.Red);
            WriteError(ex.Message);
            return ExitCodeLoadOrOtherError;
        }
    }

    private static BasicRecipeValidator CreateValidator()
    {
        // The registry is immutable, so we can safely reuse it across runs, and just
        // provide a lookup function to the validator.
        return new BasicRecipeValidator(techniqueName => TechniqueRegistry.GetByName(techniqueName) is not null);
    }

    #region Console helpers

    private static void WriteSuccess(string message) => WriteLineWithColor(message, ConsoleColor.Green);

    private static void WriteError(string message) => WriteLineWithColor(message, ConsoleColor.Red);

    private static void WriteDetail(string message) => WriteLineWithColor(message, ConsoleColor.DarkGray);

    private static void WriteHeader(string message, ConsoleColor color) => WriteLineWithColor(message, color);

    private static void WriteBullet(string message, ConsoleColor color) => WriteLineWithColor($" - {message}", color);

    private static void WriteLineWithColor(string message, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = original;
    }

    #endregion
}
