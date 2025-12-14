using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Performs payload-specific preflight checks.
/// </summary>
public interface IPayloadPreflightChecker
{
    PayloadPreflightResult Check(RunewireRecipe recipe);
}
