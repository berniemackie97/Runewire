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
}
