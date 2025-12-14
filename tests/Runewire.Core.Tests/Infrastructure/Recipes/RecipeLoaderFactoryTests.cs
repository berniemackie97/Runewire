using Runewire.Core.Infrastructure.Recipes;

namespace Runewire.Core.Tests.Infrastructure.Recipes;

public sealed class RecipeLoaderFactoryTests
{
    [Theory]
    [InlineData("recipe.yaml", typeof(YamlRecipeLoader))]
    [InlineData("recipe.yml", typeof(YamlRecipeLoader))]
    [InlineData("recipe.json", typeof(JsonRecipeLoader))]
    public void CreateForPath_returns_expected_loader(string path, Type expectedType)
    {
        IRecipeLoader loader = RecipeLoaderFactory.CreateForPath(path);

        Assert.IsAssignableFrom(expectedType, loader);
    }

    [Fact]
    public void CreateForPath_throws_for_unknown_extension()
    {
        Assert.Throws<RecipeLoadException>(() => RecipeLoaderFactory.CreateForPath("recipe.txt"));
    }
}
