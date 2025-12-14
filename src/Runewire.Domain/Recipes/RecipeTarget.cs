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

    /// <summary>
    /// Path to executable when Kind is LaunchProcess. Otherwise null.
    /// </summary>
    public string? LaunchPath { get; init; }

    /// <summary>
    /// Command line arguments when Kind is LaunchProcess.
    /// </summary>
    public string? LaunchArguments { get; init; }

    /// <summary>
    /// Working directory when Kind is LaunchProcess.
    /// </summary>
    public string? LaunchWorkingDirectory { get; init; }

    /// <summary>
    /// Start suspended when Kind is LaunchProcess.
    /// </summary>
    public bool LaunchStartSuspended { get; init; }

    private RecipeTarget(RecipeTargetKind kind, int? processId, string? processName, string? launchPath, string? launchArgs, string? launchWorkingDir, bool launchStartSuspended)
    {
        Kind = kind;
        ProcessId = processId;
        ProcessName = processName;
        LaunchPath = launchPath;
        LaunchArguments = launchArgs;
        LaunchWorkingDirectory = launchWorkingDir;
        LaunchStartSuspended = launchStartSuspended;
    }

    /// <summary>
    /// Target the current Runewire process.
    /// </summary>
    public static RecipeTarget Self() => new(RecipeTargetKind.Self, null, null, null, null, null, false);

    /// <summary>
    /// Target a specific process id.
    /// </summary>
    public static RecipeTarget ForProcessId(int processId) => new(RecipeTargetKind.ProcessById, processId, null, null, null, null, false);

    /// <summary>
    /// Target a process by name.
    /// </summary>
    public static RecipeTarget ForProcessName(string processName) => new(RecipeTargetKind.ProcessByName, null, processName, null, null, null, false);

    /// <summary>
    /// Launch a new process as the target.
    /// </summary>
    public static RecipeTarget ForLaunchProcess(string path, string? arguments = null, string? workingDirectory = null, bool startSuspended = false) =>
        new(RecipeTargetKind.LaunchProcess, null, null, path, arguments, workingDirectory, startSuspended);
}
