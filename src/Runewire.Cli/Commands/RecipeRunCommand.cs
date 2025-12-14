using System.CommandLine;
using Runewire.Cli.Infrastructure;
using Runewire.Cli.Infrastructure.Output;
using Runewire.Core.Infrastructure.Recipes;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Orchestration;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;
using Runewire.Orchestrator.Infrastructure.Preflight;
using Runewire.Orchestrator.Infrastructure.Services;
using System.Text.Json;

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

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates the run command:
    ///   runewire run recipe.yaml
    ///   runewire run --native recipe.yaml
    /// </summary>
    public static Command Create()
    {
        Argument<FileInfo> recipeArgument = new("recipe")
        {
            Description = "Path to the recipe file (YAML or JSON) to execute.",
        };

        // This prevents the parser from treating the description as an alias.
        Option<bool> nativeOption = new("--native")
        {
            Description = "Use the native Runewire.Injector engine instead of the dry-run engine.",
        };

        Option<bool> jsonOption = new("--json")
        {
            Description = "Emit machine-readable JSON output instead of human-readable text.",
        };

        Command command = new(name: CommandName, description: "Execute a Runewire recipe file (dry-run injection engine by default).")
        {
            recipeArgument,
            nativeOption,
            jsonOption,
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            FileInfo? recipeFile = parseResult.GetValue(recipeArgument);
            bool useNativeEngine = parseResult.GetValue(nativeOption);
            bool outputJson = parseResult.GetValue(jsonOption);

            if (recipeFile is null)
            {
                if (outputJson)
                {
                    WriteJson(JsonResponseFactory.RunError("unknown", useNativeEngine ? "native" : "dry-run", "No recipe file specified."));
                }
                else
                {
                    CliConsole.WriteError("No recipe file specified.");
                }
                return Task.FromResult(ExitCodeLoadError);
            }

            return HandleAsync(recipeFile, useNativeEngine, outputJson, cancellationToken);
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
    private static async Task<int> HandleAsync(FileInfo recipeFile, bool useNativeEngine, bool outputJson, CancellationToken cancellationToken)
    {
        if (!recipeFile.Exists)
        {
            if (outputJson)
            {
                WriteJson(JsonResponseFactory.RunError(recipeFile.Name, useNativeEngine ? "native" : "dry-run", $"Recipe file not found: {recipeFile.FullName}"));
            }
            else
            {
                CliConsole.WriteError($"Recipe file not found: {recipeFile.FullName}");
            }
            return ExitCodeLoadError;
        }

        cancellationToken.ThrowIfCancellationRequested();

        RecipeExecutionService service = new(new DefaultRecipeLoaderProvider(), new ProcessTargetPreflightChecker(), new PayloadPreflightChecker(), new InjectionEngineFactory());
        InjectionEngineOptions? engineOptions = (!useNativeEngine && outputJson) ? new InjectionEngineOptions(TextWriter.Null) : null;

        try
        {
            RecipeRunOutcome outcome = await service.RunAsync(recipeFile.FullName, useNativeEngine, engineOptions, cancellationToken).ConfigureAwait(false);
            RunewireRecipe recipe = outcome.Recipe;
            InjectionResult result = outcome.InjectionResult;

            if (useNativeEngine && !outputJson)
            {
                // EngineSelectionTests asserts this line. Keep it stable.
                CliConsole.WriteDetail($"Using native injection engine for recipe '{recipe.Name}'.");
            }

            if (result.Success)
            {
                if (outputJson)
                {
                    WriteJson(JsonResponseFactory.RunSuccess(outcome));
                }
                else
                {
                    CliConsole.WriteSuccess($"Injection succeeded for recipe '{recipe.Name}'.");
                }
                return ExitCodeSuccess;
            }

            if (outputJson)
            {
                WriteJson(JsonResponseFactory.RunFailure(outcome));
            }
            else
            {
                CliConsole.WriteHeader("Injection failed.", ConsoleColor.Red);

                if (!string.IsNullOrWhiteSpace(result.ErrorCode))
                {
                    CliConsole.WriteError($"Code: {result.ErrorCode}");
                }

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    CliConsole.WriteError(result.ErrorMessage!);
                }
            }

            return ExitCodeInjectionFailure;
        }
        catch (RecipeLoadException ex)
        {
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
            return ExitCodeLoadError;
        }
        catch (Exception ex)
        {
            if (outputJson)
            {
                WriteJson(JsonResponseFactory.RunError(recipeFile.Name, useNativeEngine ? "native" : "dry-run", ex.Message));
            }
            else
            {
                CliConsole.WriteHeader("Unexpected error while executing recipe.", ConsoleColor.Red);
                CliConsole.WriteError(ex.Message);
            }
            return ExitCodeInjectionFailure;
        }
    }

    private static void WriteJson(object payload)
    {
        string json = JsonSerializer.Serialize(payload, s_jsonOptions);
        Console.WriteLine(json);
    }
}
