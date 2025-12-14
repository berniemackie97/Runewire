using Runewire.Core.Infrastructure.Validation;
using Runewire.Domain.Validation;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Factory that builds the right recipe loader with the default validator wiring.
/// Frontends (CLI, Studio, Server) should come through here to stay consistent.
/// </summary>
public static class RecipeLoaderFactory
{
    public static IRecipeLoader CreateForPath(string path)
    {
        IRecipeValidator validator = RecipeValidatorFactory.CreateDefaultValidator();
        return RecipeLoaderSelector.CreateForPath(path, validator);
    }
}
