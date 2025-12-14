using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Core.Infrastructure.Recipes;
using Runewire.Core.Infrastructure.Validation;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Orchestration;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;

namespace Runewire.Cli.Commands;

/// <summary>
/// Runs a recipe using the injection engine.
/// Dry run by default unless --native is set.
/// </summary>
public static class RecipeRunCommand
{
    public const string CommandName = "run";

    // Exit codes for run.
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeValidationError = 1;
    private const int ExitCodeLoadError = 2;
    private const int ExitCodeInjectionFailure = 3;

    /// <summary>
    /// Creates the run command:
    ///   runewire run recipe.yaml
    ///   runewire run --native recipe.yaml
    /// </summary>
    public static Command Create()
    {
        Argument<FileInfo> recipeArgument = new("recipe")
        {
            Description = "Path to the recipe YAML file to execute.",
        };

        // This prevents the parser from treating the description as an alias.
        Option<bool> nativeOption = new("--native")
        {
            Description = "Use the native Runewire.Injector engine instead of the dry-run engine.",
        };

        Command command = new(name: CommandName, description: "Execute a Runewire recipe (dry-run injection engine by default).")
        {
            recipeArgument,
            nativeOption,
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            FileInfo? recipeFile = parseResult.GetValue(recipeArgument);
            bool useNativeEngine = parseResult.GetValue(nativeOption);

            if (recipeFile is null)
            {
                CliConsole.WriteError("No recipe file specified.");
                return Task.FromResult(ExitCodeLoadError);
            }

            return HandleAsync(recipeFile, useNativeEngine, cancellationToken);
        });

        return command;
    }

    /// <summary>
    /// Runs the recipe and returns an exit code.
    ///
    /// Exit codes:
    /// 0 = injection succeeded
    /// 1 = validation errors (semantic)
    /// 2 = load/structural error (IO, YAML parse)
    /// 3 = injection failed (engine reported failure or unexpected error)
    /// </summary>
    private static async Task<int> HandleAsync(FileInfo recipeFile, bool useNativeEngine, CancellationToken cancellationToken)
    {
        if (!recipeFile.Exists)
        {
            CliConsole.WriteError($"Recipe file not found: {recipeFile.FullName}");
            return ExitCodeLoadError;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Shared validator factory so CLI / Studio / Server stay in sync.
        BasicRecipeValidator validator = RecipeValidatorFactory.CreateDefaultValidator();
        YamlRecipeLoader loader = new(validator);

        IInjectionEngine engine = CreateInjectionEngine(useNativeEngine);
        RecipeExecutor executor = new(engine);

        try
        {
            RunewireRecipe recipe = loader.LoadFromFile(recipeFile.FullName);

            if (useNativeEngine)
            {
                // EngineSelectionTests asserts this line. Keep it stable.
                CliConsole.WriteDetail($"Using native injection engine for recipe '{recipe.Name}'.");
            }

            InjectionResult result = await executor.ExecuteAsync(recipe, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                CliConsole.WriteSuccess($"Injection succeeded for recipe '{recipe.Name}'.");
                return ExitCodeSuccess;
            }

            CliConsole.WriteHeader("Injection failed.", ConsoleColor.Red);

            if (!string.IsNullOrWhiteSpace(result.ErrorCode))
            {
                CliConsole.WriteError($"Code: {result.ErrorCode}");
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                CliConsole.WriteError(result.ErrorMessage!);
            }

            return ExitCodeInjectionFailure;
        }
        catch (RecipeLoadException ex)
        {
            if (ex.ValidationErrors.Count > 0)
            {
                CliConsole.WriteHeader("Recipe is invalid.", ConsoleColor.Yellow);
                foreach (RecipeValidationError error in ex.ValidationErrors)
                {
                    CliConsole.WriteBullet($"[{error.Code}] {error.Message}", ConsoleColor.Yellow);
                }

                return ExitCodeValidationError;
            }

            CliConsole.WriteHeader("Failed to load recipe.", ConsoleColor.Red);
            CliConsole.WriteError(ex.Message);

            if (ex.InnerException is not null)
            {
                CliConsole.WriteDetail($"Inner: {ex.InnerException.Message}");
            }

            return ExitCodeLoadError;
        }
        catch (Exception ex)
        {
            CliConsole.WriteHeader("Unexpected error while executing recipe.", ConsoleColor.Red);
            CliConsole.WriteError(ex.Message);
            return ExitCodeInjectionFailure;
        }
    }

    private static IInjectionEngine CreateInjectionEngine(bool useNativeEngine) => useNativeEngine ? new NativeInjectionEngine() : new DryRunInjectionEngine();
}
