using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Executes a validated recipe by mapping it into an InjectionRequest and sending it to the engine.
/// </summary>
public sealed class RecipeExecutor(IInjectionEngine injectionEngine)
{
    private readonly IInjectionEngine _injectionEngine = injectionEngine ?? throw new ArgumentNullException(nameof(injectionEngine));

    /// <summary>
    /// Execute a recipe using the configured engine.
    /// </summary>
    public Task<InjectionResult> ExecuteAsync(RunewireRecipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        cancellationToken.ThrowIfCancellationRequested();

        InjectionRequest request = MapToRequest(recipe);
        return _injectionEngine.ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Convert the domain recipe into the engine request shape.
    /// </summary>
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
