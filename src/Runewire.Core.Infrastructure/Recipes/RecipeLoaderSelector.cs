using Runewire.Domain.Validation;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Picks the right recipe loader based on file extension.
/// </summary>
public static class RecipeLoaderSelector
{
    /// <summary>
    /// Create a loader for the provided recipe path.
    /// </summary>
    public static IRecipeLoader CreateForPath(string path, IRecipeValidator validator)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be null or whitespace.", nameof(path));
        }

        ArgumentNullException.ThrowIfNull(validator);

        string extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".yaml" or ".yml" => new YamlRecipeLoader(validator),
            ".json" => new JsonRecipeLoader(validator),
            _ => throw new RecipeLoadException($"Unsupported recipe file extension '{extension}'. Expected .yaml, .yml, or .json."),
        };
    }
}
