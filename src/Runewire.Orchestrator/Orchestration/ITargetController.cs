using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Controls target process state for workflow steps (suspend/resume).
/// </summary>
public interface ITargetController
{
    Task<TargetControlResult> SuspendAsync(RecipeTarget target, CancellationToken cancellationToken = default);

    Task<TargetControlResult> ResumeAsync(RecipeTarget target, CancellationToken cancellationToken = default);
}
