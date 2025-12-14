using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Result of executing a single recipe step.
/// </summary>
public sealed record RecipeStepResult(
    int Index,
    RecipeStepKind Kind,
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    InjectionResult? InjectionResult = null)
{
    public static RecipeStepResult FromInjection(int index, RecipeStep step, InjectionResult result)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(result);

        return new RecipeStepResult(
            index,
            step.Kind,
            result.Success,
            result.ErrorCode,
            result.ErrorMessage,
            result.StartedAtUtc,
            result.CompletedAtUtc,
            result);
    }

    public static RecipeStepResult FromControl(int index, RecipeStep step, TargetControlResult controlResult, DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(controlResult);

        return new RecipeStepResult(
            index,
            step.Kind,
            controlResult.Success,
            controlResult.ErrorCode,
            controlResult.ErrorMessage,
            startedAtUtc,
            completedAtUtc,
            null);
    }

    public static RecipeStepResult Wait(int index, RecipeStep step, DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(step);

        return new RecipeStepResult(
            index,
            step.Kind,
            true,
            null,
            null,
            startedAtUtc,
            completedAtUtc,
            null);
    }
}
