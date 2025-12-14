namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Creates recipe loaders based on path.
/// </summary>
public interface IRecipeLoaderProvider
{
    IRecipeLoader Create(string path);
}
