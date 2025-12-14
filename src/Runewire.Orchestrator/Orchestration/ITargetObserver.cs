using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Observes target state to satisfy wait conditions.
/// </summary>
public interface ITargetObserver
{
    Task<WaitResult> WaitForAsync(RecipeTarget target, WaitCondition condition, CancellationToken cancellationToken = default);
}
