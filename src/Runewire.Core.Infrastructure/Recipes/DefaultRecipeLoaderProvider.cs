namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Uses RecipeLoaderFactory to create loaders.
/// </summary>
public sealed class DefaultRecipeLoaderProvider : IRecipeLoaderProvider
{
    public IRecipeLoader Create(string path) => RecipeLoaderFactory.CreateForPath(path);
}
