using Runewire.Core.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// High-level orchestrator that executes validated recipes by delegating
/// to the configured <see cref="IInjectionEngine"/>.
/// </summary>
public sealed class RecipeExecutor(IInjectionEngine injectionEngine)
{
    private readonly IInjectionEngine _injectionEngine = injectionEngine ?? throw new ArgumentNullException(nameof(injectionEngine));

    /// <summary>
    /// Execute a validated recipe using the injection engine.
    /// </summary>
    public Task<InjectionResult> ExecuteAsync(RunewireRecipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        InjectionRequest request = MapToRequest(recipe);
        return _injectionEngine.ExecuteAsync(request, cancellationToken);
    }

    private static InjectionRequest MapToRequest(RunewireRecipe recipe)
    {
        return new InjectionRequest(
            recipe.Name,
            recipe.Description,
            recipe.Target,
            recipe.Technique.Name,
            recipe.PayloadPath,
            recipe.AllowKernelDrivers,
            recipe.RequireInteractiveConsent
        );
    }
}
