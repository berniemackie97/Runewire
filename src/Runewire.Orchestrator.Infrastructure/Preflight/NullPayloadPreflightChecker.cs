using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

internal sealed class NullPayloadPreflightChecker : IPayloadPreflightChecker
{
    public PayloadPreflightResult Check(RunewireRecipe recipe) => PayloadPreflightResult.Ok(null, null);
}
