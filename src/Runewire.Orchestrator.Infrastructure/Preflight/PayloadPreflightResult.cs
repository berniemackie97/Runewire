using Runewire.Domain.Validation;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Result of payload preflight checks.
/// </summary>
public sealed record PayloadPreflightResult(bool Success, IReadOnlyList<RecipeValidationError> Errors, string? PayloadArchitecture, string? ProcessArchitecture)
{
    public static PayloadPreflightResult Ok(string? payloadArchitecture = null, string? processArchitecture = null) =>
        new(true, [], payloadArchitecture, processArchitecture);

    public static PayloadPreflightResult Failed(string? payloadArchitecture, string? processArchitecture, params RecipeValidationError[] errors) =>
        new(false, errors ?? [], payloadArchitecture, processArchitecture);
}
