namespace Runewire.Domain.Recipes;

/// <summary>
/// Target for a recipe.
/// </summary>
public sealed record RecipeTarget
{
    /// <summary>
    /// How the target process is picked.
    /// </summary>
    public RecipeTargetKind Kind { get; init; }

    /// <summary>
    /// Process id when Kind is ProcessById. Otherwise null.
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>
    /// Process name when Kind is ProcessByName. Otherwise null.
    /// </summary>
    public string? ProcessName { get; init; }

    private RecipeTarget(RecipeTargetKind kind, int? processId, string? processName)
    {
        Kind = kind;
        ProcessId = processId;
        ProcessName = processName;
    }

    /// <summary>
    /// Target the current Runewire process.
    /// </summary>
    public static RecipeTarget Self() => new(RecipeTargetKind.Self, null, null);

    /// <summary>
    /// Target a specific process id.
    /// </summary>
    public static RecipeTarget ForProcessId(int processId) => new(RecipeTargetKind.ProcessById, processId, null);

    /// <summary>
    /// Target a process by name.
    /// </summary>
    public static RecipeTarget ForProcessName(string processName) => new(RecipeTargetKind.ProcessByName, null, processName);
}
