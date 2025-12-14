using Runewire.Orchestrator.Infrastructure.Preflight;

namespace Runewire.Orchestrator.Infrastructure.Services;

internal static class PreflightSummaryBuilder
{
    public static PreflightSummary Build(TargetPreflightResult target, PayloadPreflightResult payload)
    {
        return new PreflightSummary(
            target.Success,
            target.Errors,
            payload.Success,
            payload.Errors,
            payload.PayloadArchitecture,
            payload.ProcessArchitecture);
    }
}
