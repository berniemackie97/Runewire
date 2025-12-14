using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Performs environment checks before executing an injection for a target.
/// </summary>
public interface ITargetPreflightChecker
{
    TargetPreflightResult Check(RunewireRecipe recipe);
}
