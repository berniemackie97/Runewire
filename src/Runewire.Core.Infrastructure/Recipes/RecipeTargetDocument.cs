namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Raw target section from YAML.
/// Loader maps this into the domain target model after validation.
/// </summary>
internal sealed class RecipeTargetDocument
{
    /// <summary>
    /// Target kind from YAML. Loader maps this into RecipeTargetKind.
    /// Expected values right now:
    /// self
    /// processById
    /// processByName
    /// launchProcess
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// Used when Kind is processById.
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// Used when Kind is processByName.
    /// </summary>
    public string? ProcessName { get; set; }

    /// <summary>
    /// Used when Kind is launchProcess.
    /// </summary>
    public string? Path { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public bool? StartSuspended { get; set; }
}
