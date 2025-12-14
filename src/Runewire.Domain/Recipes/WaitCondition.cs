namespace Runewire.Domain.Recipes;

/// <summary>
/// Condition used by a wait step.
/// </summary>
public sealed record WaitCondition(WaitConditionKind Kind, string Value, int? TimeoutMilliseconds = null);
