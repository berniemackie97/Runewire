using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Target observer that always reports unsupported.
/// </summary>
public sealed class NullTargetObserver : ITargetObserver
{
    public Task<WaitResult> WaitForAsync(RecipeTarget target, WaitCondition condition, CancellationToken cancellationToken = default) =>
        Task.FromResult(WaitResult.Failed("WAIT_CONDITION_UNAVAILABLE", "Wait conditions are not enabled for this host."));
}
