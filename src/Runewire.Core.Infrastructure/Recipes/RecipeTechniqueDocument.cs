namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Raw technique section from YAML.
/// Loader maps this into the domain technique after validation.
/// </summary>
internal sealed class RecipeTechniqueDocument
{
    /// <summary>
    /// Technique name from the recipe.
    /// This is the lookup key for the technique registry.
    /// </summary>
    public string? Name { get; set; }
}
