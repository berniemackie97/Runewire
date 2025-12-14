namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Loads RunewireRecipe from YAML.
/// This is the boundary where text turns into a validated domain object.
/// </summary>
public interface IYamlRecipeLoader : IRecipeLoader
{
    // Inherits from IRecipeLoader for shared semantics.
}
