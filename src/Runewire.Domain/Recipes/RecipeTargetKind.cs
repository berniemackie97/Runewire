namespace Runewire.Domain.Recipes;

/// <summary>
/// How the recipe picks the target process.
/// </summary>
public enum RecipeTargetKind
{
    /// <summary>
    /// Target the current Runewire process.
    /// </summary>
    Self = 0,

    /// <summary>
    /// Target a specific process id.
    /// </summary>
    ProcessById = 1,

    /// <summary>
    /// Target by process name match.
    /// </summary>
    ProcessByName = 2,

    /// <summary>
    /// Launch a new process as the target.
    /// </summary>
    LaunchProcess = 3,
}
