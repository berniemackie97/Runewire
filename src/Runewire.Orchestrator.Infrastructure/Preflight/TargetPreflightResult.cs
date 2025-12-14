using Runewire.Domain.Validation;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Result of a target preflight check.
/// </summary>
public sealed record TargetPreflightResult(bool Success, IReadOnlyList<RecipeValidationError> Errors)
{
    public static TargetPreflightResult Ok() => new(true, Array.Empty<RecipeValidationError>());

    public static TargetPreflightResult Failed(params RecipeValidationError[] errors) => new(false, errors ?? []);
}
