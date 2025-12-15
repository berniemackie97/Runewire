using Runewire.Core.Infrastructure.Recipes;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;
using Runewire.Orchestrator.Infrastructure.Preflight;
using Runewire.Orchestrator.Infrastructure.Targets;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Services;

/// <summary>
/// Shared service to load, preflight, and execute recipes.
/// CLI/Studio/Server should come through here to keep behavior consistent.
/// </summary>
public sealed class RecipeExecutionService(IRecipeLoaderProvider loaderProvider, ITargetPreflightChecker targetPreflightChecker,
    IPayloadPreflightChecker payloadPreflightChecker, IInjectionEngineFactory engineFactory, NativeVersionPreflightChecker? nativeVersionPreflightChecker = null,
    ITargetController? targetController = null, ITargetObserver? targetObserver = null)
{
    private readonly IRecipeLoaderProvider _loaderProvider = loaderProvider ?? throw new ArgumentNullException(nameof(loaderProvider));
    private readonly ITargetPreflightChecker _targetPreflightChecker = targetPreflightChecker ?? throw new ArgumentNullException(nameof(targetPreflightChecker));
    private readonly IPayloadPreflightChecker _payloadPreflightChecker = payloadPreflightChecker ?? throw new ArgumentNullException(nameof(payloadPreflightChecker));
    private readonly IInjectionEngineFactory _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
    private readonly NativeVersionPreflightChecker? _nativeVersionPreflightChecker = nativeVersionPreflightChecker;
    private readonly ITargetController _targetController = targetController ?? new ProcessTargetController();
    private readonly ITargetObserver _targetObserver = targetObserver ?? CreateDefaultObserver();

    /// <summary>
    /// Load and validate a recipe from the provided path.
    /// Throws RecipeLoadException on validation/preflight failures.
    /// </summary>
    public RecipeValidationOutcome Validate(string path)
    {
        IRecipeLoader loader = _loaderProvider.Create(path);
        RunewireRecipe recipe = NormalizePaths(path, loader.LoadFromFile(path));

        TargetPreflightResult targetPreflight = _targetPreflightChecker.Check(recipe);
        if (!targetPreflight.Success)
        {
            ThrowValidation(targetPreflight.Errors);
        }

        PayloadPreflightResult payloadPreflight = _payloadPreflightChecker.Check(recipe);
        if (!payloadPreflight.Success)
        {
            ThrowValidation(payloadPreflight.Errors);
        }

        IReadOnlyList<RecipeValidationError> versionErrors = CheckNativeVersion(recipe);
        if (versionErrors.Count > 0)
        {
            ThrowValidation(versionErrors);
        }

        PreflightSummary preflight = PreflightSummaryBuilder.Build(targetPreflight, payloadPreflight);

        return new RecipeValidationOutcome(recipe, targetPreflight, payloadPreflight, preflight);
    }

    /// <summary>
    /// Execute a recipe from path using the chosen engine.
    /// Throws RecipeLoadException on validation/preflight failures.
    /// </summary>
    public async Task<RecipeRunOutcome> RunAsync(string path, bool useNativeEngine, InjectionEngineOptions? engineOptions = null, CancellationToken cancellationToken = default)
    {
        RecipeValidationOutcome validation = Validate(path);
        RunewireRecipe recipe = validation.Recipe;

        IInjectionEngine engine = _engineFactory.Create(useNativeEngine, engineOptions);
        RecipeExecutor executor = new(engine, _targetController, _targetObserver);

        RecipeExecutionResult executionResult = await executor.ExecuteAsync(recipe, cancellationToken).ConfigureAwait(false);
        return new RecipeRunOutcome(recipe, executionResult.OverallResult, executionResult.StepResults, useNativeEngine ? "native" : "dry-run", validation.Preflight);
    }

    private static void ThrowValidation(IEnumerable<RecipeValidationError> errors)
    {
        List<RecipeValidationError> list = errors?.ToList() ?? [];
        throw new RecipeLoadException("Recipe failed preflight.", list);
    }

    private IReadOnlyList<RecipeValidationError> CheckNativeVersion(RunewireRecipe recipe)
    {
        if (_nativeVersionPreflightChecker is null)
        {
            return Array.Empty<RecipeValidationError>();
        }

        HashSet<string> techniqueNames = new(StringComparer.OrdinalIgnoreCase)
        {
            recipe.Technique.Name
        };

        if (recipe.Steps is not null)
        {
            foreach (RecipeStep step in recipe.Steps.Where(s => s.Kind == RecipeStepKind.InjectTechnique && !string.IsNullOrWhiteSpace(s.TechniqueName)))
            {
                techniqueNames.Add(step.TechniqueName!);
            }
        }

        List<RecipeValidationError> errors = [];
        foreach (string techniqueName in techniqueNames)
        {
            errors.AddRange(_nativeVersionPreflightChecker.Check(techniqueName));
        }
        return errors;
    }

    private static ITargetObserver CreateDefaultObserver()
    {
        return OperatingSystem.IsWindows()
            ? new ProcessTargetObserver()
            : new UnixTargetObserver();
    }

    private static RunewireRecipe NormalizePaths(string recipePath, RunewireRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        string recipeDirectory = Path.GetDirectoryName(recipePath) ?? Directory.GetCurrentDirectory();

        string Normalize(string path) =>
            string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(recipeDirectory, path));

        string normalizedPayload = Normalize(recipe.PayloadPath);

        IReadOnlyList<RecipeStep>? normalizedSteps = recipe.Steps;
        if (recipe.Steps is not null && recipe.Steps.Count > 0)
        {
            List<RecipeStep> steps = new(recipe.Steps.Count);
            foreach (RecipeStep step in recipe.Steps)
            {
                string? stepPayload = step.PayloadPath;
                if (!string.IsNullOrWhiteSpace(stepPayload))
                {
                    stepPayload = Normalize(stepPayload);
                }

                RecipeStep normalized = step with { PayloadPath = stepPayload };
                steps.Add(normalized);
            }
            normalizedSteps = steps;
        }

        return recipe with { PayloadPath = normalizedPayload, Steps = normalizedSteps };
    }
}
