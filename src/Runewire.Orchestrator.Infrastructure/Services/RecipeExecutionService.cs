using Runewire.Core.Infrastructure.Recipes;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;
using Runewire.Orchestrator.Infrastructure.Preflight;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Services;

/// <summary>
/// Shared service to load, preflight, and execute recipes.
/// CLI/Studio/Server should come through here to keep behavior consistent.
/// </summary>
public sealed class RecipeExecutionService(IRecipeLoaderProvider loaderProvider, ITargetPreflightChecker targetPreflightChecker,
    IPayloadPreflightChecker payloadPreflightChecker, IInjectionEngineFactory engineFactory)
{
    private readonly IRecipeLoaderProvider _loaderProvider = loaderProvider ?? throw new ArgumentNullException(nameof(loaderProvider));
    private readonly ITargetPreflightChecker _targetPreflightChecker = targetPreflightChecker ?? throw new ArgumentNullException(nameof(targetPreflightChecker));
    private readonly IPayloadPreflightChecker _payloadPreflightChecker = payloadPreflightChecker ?? throw new ArgumentNullException(nameof(payloadPreflightChecker));
    private readonly IInjectionEngineFactory _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));

    /// <summary>
    /// Load and validate a recipe from the provided path (including preflight).
    /// Throws RecipeLoadException on validation/preflight failures.
    /// </summary>
    public RecipeValidationOutcome Validate(string path)
    {
        IRecipeLoader loader = _loaderProvider.Create(path);
        RunewireRecipe recipe = loader.LoadFromFile(path);

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
        RecipeExecutor executor = new(engine);

        InjectionResult result = await executor.ExecuteAsync(recipe, cancellationToken).ConfigureAwait(false);
        return new RecipeRunOutcome(recipe, result, useNativeEngine ? "native" : "dry-run", validation.Preflight);
    }

    private static void ThrowValidation(IEnumerable<RecipeValidationError> errors)
    {
        List<RecipeValidationError> list = errors?.ToList() ?? [];
        throw new RecipeLoadException("Recipe failed preflight.", list);
    }
}
