using Runewire.Domain.Recipes;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Loads a Runewire recipe from text or file and validates it.
/// </summary>
public interface IRecipeLoader
{
    /// <summary>
    /// Parse text, map it into a recipe, then validate it.
    /// </summary>
    RunewireRecipe LoadFromString(string text);

    /// <summary>
    /// Read a file, parse it, map it into a recipe, then validate it.
    /// </summary>
    RunewireRecipe LoadFromFile(string path);
}
