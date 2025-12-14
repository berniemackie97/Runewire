namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Result of executing a recipe workflow.
/// </summary>
public sealed record RecipeExecutionResult(InjectionResult OverallResult, IReadOnlyList<RecipeStepResult> StepResults);
