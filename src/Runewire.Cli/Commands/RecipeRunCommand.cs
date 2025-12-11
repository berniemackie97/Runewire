using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Core.Domain.Recipes;
using Runewire.Core.Domain.Validation;
using Runewire.Core.Infrastructure.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Cli.Commands;

/// <summary>
/// CLI command for executing a recipe using the injection engine.
/// </summary>
public static class RecipeRunCommand
{
    public const string CommandName = "run";

    /// <summary>
    /// Creates the 'run' command:
    ///   runewire run <recipe.yaml>
    ///   runewire run --native <recipe.yaml>
    /// </summary>
    public static Command Create()
    {
        Argument<FileInfo> recipeArgument = new("recipe")
        {
            Description = "Path to the recipe YAML file to execute.",
        };

        // IMPORTANT:
        // Your System.CommandLine version's Option<T> ctors are touchy.
        // We avoid all overload ambiguity by:
        //   - Using a single string name
        //   - Setting Description via property
        //
        // This guarantees "--native" is parsed as the option name and
        // we don't accidentally treat descriptions as aliases.
        Option<bool> nativeOption = new("--native")
        {
            Description = "Use the native Runewire.Injector engine instead of the dry-run engine.",
        };

        Command command = new(
            name: CommandName,
            description: "Execute a Runewire recipe (dry-run injection engine by default)."
        )
        {
            recipeArgument,
            nativeOption,
        };

        command.SetAction(parseResult =>
        {
            FileInfo? recipeFile = parseResult.GetValue(recipeArgument);
            bool useNativeEngine = parseResult.GetValue(nativeOption);

            if (recipeFile is null)
            {
                WriteError("No recipe file specified.");
                return 2;
            }

            return Handle(recipeFile, useNativeEngine);
        });

        return command;
    }

    /// <summary>
    /// Core handler logic for running a recipe.
    ///
    /// Exit codes:
    /// 0 = injection succeeded
    /// 1 = validation errors (semantic)
    /// 2 = load/structural error (I/O, YAML parse)
    /// 3 = injection failed (engine reported failure or unexpected error)
    /// </summary>
    private static int Handle(FileInfo recipeFile, bool useNativeEngine)
    {
        if (!recipeFile.Exists)
        {
            WriteError($"Recipe file not found: {recipeFile.FullName}");
            return 2;
        }

        BasicRecipeValidator validator = new();
        YamlRecipeLoader loader = new(validator);

        IInjectionEngine engine = useNativeEngine
            ? new NativeInjectionEngine()
            : new DryRunInjectionEngine();

        RecipeExecutor executor = new(engine);

        try
        {
            RunewireRecipe recipe = loader.LoadFromFile(recipeFile.FullName);

            if (useNativeEngine)
            {
                // This string is asserted in EngineSelectionTests.
                WriteDetail($"Using native injection engine for recipe '{recipe.Name}'.");
            }

            InjectionResult result = executor.ExecuteAsync(recipe).GetAwaiter().GetResult();

            if (result.Success)
            {
                WriteSuccess($"Injection succeeded for recipe '{recipe.Name}'.");
                return 0;
            }

            WriteHeader("Injection failed.", ConsoleColor.Red);
            if (!string.IsNullOrWhiteSpace(result.ErrorCode))
            {
                WriteError($"Code: {result.ErrorCode}");
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                WriteError(result.ErrorMessage!);
            }

            return 3;
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

                return 1;
            }

            WriteHeader("Failed to load recipe.", ConsoleColor.Red);
            WriteError(ex.Message);

            if (ex.InnerException is not null)
            {
                WriteDetail($"Inner: {ex.InnerException.Message}");
            }

            return 2;
        }
        catch (Exception ex)
        {
            WriteHeader("Unexpected error while executing recipe.", ConsoleColor.Red);
            WriteError(ex.Message);
            return 3;
        }
    }

    #region Console helpers

    private static void WriteSuccess(string message) =>
        WriteLineWithColor(message, ConsoleColor.Green);

    private static void WriteError(string message) => WriteLineWithColor(message, ConsoleColor.Red);

    private static void WriteDetail(string message) =>
        WriteLineWithColor(message, ConsoleColor.DarkGray);

    private static void WriteHeader(string message, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = original;
    }

    private static void WriteBullet(string message, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($" - {message}");
        Console.ForegroundColor = original;
    }

    private static void WriteLineWithColor(string message, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = original;
    }

    #endregion
}
