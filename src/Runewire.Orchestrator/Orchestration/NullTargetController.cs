using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Target controller that reports unsupported for suspend/resume.
/// Useful for hosts that only run inject steps.
/// </summary>
public sealed class NullTargetController : ITargetController
{
    public Task<TargetControlResult> ResumeAsync(RecipeTarget target, CancellationToken cancellationToken = default) =>
        Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_UNAVAILABLE", "Suspend/resume is not enabled for this host."));

    public Task<TargetControlResult> SuspendAsync(RecipeTarget target, CancellationToken cancellationToken = default) =>
        Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_UNAVAILABLE", "Suspend/resume is not enabled for this host."));
}
