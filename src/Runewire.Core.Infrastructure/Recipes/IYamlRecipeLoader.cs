using Runewire.Domain.Recipes;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Loads RunewireRecipe from YAML.
/// This is the boundary where text turns into a validated domain object.
/// </summary>
public interface IYamlRecipeLoader
{
    /// <summary>
    /// Parse YAML from a string, then validate the recipe.
    /// If anything is wrong (bad YAML or failed validation), throw RecipeLoadException.
    /// </summary>
    /// <param name="yaml">YAML text.</param>
    /// <returns>Validated recipe.</returns>
    RunewireRecipe LoadFromString(string yaml);

    /// <summary>
    /// Read a YAML file from disk, parse it, then validate the recipe.
    /// Throw RecipeLoadException for any load/parse/validation failure.
    /// </summary>
    /// <param name="path">Path to the YAML file.</param>
    /// <returns>Validated recipe.</returns>
    RunewireRecipe LoadFromFile(string path);
}
