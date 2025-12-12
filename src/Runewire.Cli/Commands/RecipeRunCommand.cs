using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Core.Domain.Recipes;
using Runewire.Core.Domain.Techniques;
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

    // Exit codes for the 'run' command.
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeValidationError = 1;
    private const int ExitCodeLoadError = 2;
    private const int ExitCodeInjectionFailure = 3;

    // Technique registry is immutable and safe to reuse across invocations.
    private static readonly BuiltInInjectionTechniqueRegistry TechniqueRegistry = new();

    /// <summary>
    /// Creates the 'run' command:
    ///   runewire run &lt;recipe.yaml&gt;
    ///   runewire run --native &lt;recipe.yaml&gt;
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

        Command command = new(name: CommandName, description: "Execute a Runewire recipe (dry-run injection engine by default).")
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
                return ExitCodeLoadError;
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
            return ExitCodeLoadError;
        }

        BasicRecipeValidator validator = CreateValidator();
        YamlRecipeLoader loader = new(validator);

        IInjectionEngine engine = CreateInjectionEngine(useNativeEngine);
        RecipeExecutor executor = new(engine);

        try
        {
            RunewireRecipe recipe = loader.LoadFromFile(recipeFile.FullName);

            if (useNativeEngine)
            {
                // This string is asserted in EngineSelectionTests.
                WriteDetail($"Using native injection engine for recipe '{recipe.Name}'.");
            }

            // The orchestrator is async-first. CLI handlers are currently synchronous,
            // so we bridge by blocking here. Once we move to async command handlers,
            // this can be awaited instead.
            InjectionResult result = executor.ExecuteAsync(recipe).GetAwaiter().GetResult();

            if (result.Success)
            {
                WriteSuccess($"Injection succeeded for recipe '{recipe.Name}'.");
                return ExitCodeSuccess;
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

            return ExitCodeInjectionFailure;
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

            WriteHeader("Failed to load recipe.", ConsoleColor.Red);
            WriteError(ex.Message);

            if (ex.InnerException is not null)
            {
                WriteDetail($"Inner: {ex.InnerException.Message}");
            }

            return ExitCodeLoadError;
        }
        catch (Exception ex)
        {
            WriteHeader("Unexpected error while executing recipe.", ConsoleColor.Red);
            WriteError(ex.Message);
            return ExitCodeInjectionFailure;
        }
    }

    private static IInjectionEngine CreateInjectionEngine(bool useNativeEngine) => useNativeEngine ? new NativeInjectionEngine() : new DryRunInjectionEngine();

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
