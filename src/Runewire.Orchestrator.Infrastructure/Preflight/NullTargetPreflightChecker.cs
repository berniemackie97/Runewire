using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// No-op preflight checker.
/// </summary>
internal sealed class NullTargetPreflightChecker : ITargetPreflightChecker
{
    public TargetPreflightResult Check(RunewireRecipe recipe) => TargetPreflightResult.Ok();
}
