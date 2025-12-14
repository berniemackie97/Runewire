using Runewire.Core.Infrastructure.Recipes;
using Runewire.Core.Infrastructure.Validation;
using Runewire.Domain.Validation;

namespace Runewire.Core.Tests.Infrastructure.Recipes;

public sealed class RecipeLoaderSelectorTests
{
    private static IRecipeValidator CreateValidator() => RecipeValidatorFactory.CreateDefaultValidator();

    [Theory]
    [InlineData("recipe.yaml")]
    [InlineData("recipe.yml")]
    public void CreateForPath_returns_yaml_loader_for_yaml_extensions(string path)
    {
        IRecipeLoader loader = RecipeLoaderSelector.CreateForPath(path, CreateValidator());

        Assert.IsType<YamlRecipeLoader>(loader);
    }

    [Fact]
    public void CreateForPath_returns_json_loader_for_json_extension()
    {
        IRecipeLoader loader = RecipeLoaderSelector.CreateForPath("recipe.json", CreateValidator());

        Assert.IsType<JsonRecipeLoader>(loader);
    }

    [Fact]
    public void CreateForPath_throws_on_unknown_extension()
    {
        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => RecipeLoaderSelector.CreateForPath("recipe.txt", CreateValidator()));

        Assert.Contains("Unsupported recipe file extension", ex.Message);
    }

    [Fact]
    public void CreateForPath_throws_on_empty_path()
    {
        Assert.Throws<ArgumentException>(() => RecipeLoaderSelector.CreateForPath("", CreateValidator()));
    }

    [Fact]
    public void CreateForPath_throws_on_null_validator()
    {
        Assert.Throws<ArgumentNullException>(() => RecipeLoaderSelector.CreateForPath("recipe.yaml", null!));
    }
}
