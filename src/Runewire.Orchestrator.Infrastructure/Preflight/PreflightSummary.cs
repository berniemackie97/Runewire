using Runewire.Domain.Validation;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Snapshot of preflight results for target and payload.
/// </summary>
public sealed record PreflightSummary(
    bool TargetSuccess,
    IReadOnlyList<RecipeValidationError> TargetErrors,
    bool PayloadSuccess,
    IReadOnlyList<RecipeValidationError> PayloadErrors,
    string? PayloadArchitecture,
    string? ProcessArchitecture
);
