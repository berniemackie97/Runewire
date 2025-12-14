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
public sealed class RecipeExecutionService(IRecipeLoaderProvider loaderProvider, ITargetPreflightChecker preflightChecker, IInjectionEngineFactory engineFactory)
{
    private readonly IRecipeLoaderProvider _loaderProvider = loaderProvider ?? throw new ArgumentNullException(nameof(loaderProvider));
    private readonly ITargetPreflightChecker _preflightChecker = preflightChecker ?? throw new ArgumentNullException(nameof(preflightChecker));
    private readonly IInjectionEngineFactory _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));

    /// <summary>
    /// Load and validate a recipe from the provided path (including preflight).
    /// Throws RecipeLoadException on validation/preflight failures.
    /// </summary>
    public RunewireRecipe Validate(string path)
    {
        IRecipeLoader loader = _loaderProvider.Create(path);
        RunewireRecipe recipe = loader.LoadFromFile(path);

        TargetPreflightResult preflight = _preflightChecker.Check(recipe);
        if (!preflight.Success)
        {
            ThrowValidation(preflight.Errors);
        }

        return recipe;
    }

    /// <summary>
    /// Execute a recipe from path using the chosen engine.
    /// Throws RecipeLoadException on validation/preflight failures.
    /// </summary>
    public async Task<RecipeRunOutcome> RunAsync(string path, bool useNativeEngine, CancellationToken cancellationToken = default)
    {
        RunewireRecipe recipe = Validate(path);

        IInjectionEngine engine = _engineFactory.Create(useNativeEngine);
        RecipeExecutor executor = new(engine);

        InjectionResult result = await executor.ExecuteAsync(recipe, cancellationToken).ConfigureAwait(false);
        return new RecipeRunOutcome(recipe, result, useNativeEngine ? "native" : "dry-run");
    }

    private static void ThrowValidation(IEnumerable<RecipeValidationError> errors)
    {
        List<RecipeValidationError> list = errors?.ToList() ?? [];
        throw new RecipeLoadException("Recipe failed preflight.", list);
    }
}

public sealed record RecipeRunOutcome(RunewireRecipe Recipe, InjectionResult InjectionResult, string Engine);
