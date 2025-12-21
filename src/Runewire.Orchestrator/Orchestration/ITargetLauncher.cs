using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Launches a target process for recipes that create new processes.
/// </summary>
public interface ITargetLauncher
{
    Task<TargetLaunchResult> LaunchAsync(RecipeTarget target, CancellationToken cancellationToken = default);
}
