using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Loads YAML into a RunewireRecipe, then validates it.
/// I keep parsing, mapping, and validation separated so failures are easier to reason about.
/// </summary>
public sealed class YamlRecipeLoader(IRecipeValidator validator) : IYamlRecipeLoader
{
    // Deserializer config is fixed per loader instance.
    private readonly IDeserializer _deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    private readonly IRecipeValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    /// <summary>
    /// Parse YAML from a string, map it into the domain model, then validate it.
    /// Throw RecipeLoadException when anything fails (parse, structure, or validation).
    /// </summary>
    public RunewireRecipe LoadFromString(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        RecipeDocument? document;

        try
        {
            document = _deserializer.Deserialize<RecipeDocument>(yaml);
        }
        catch (Exception ex)
        {
            throw new RecipeLoadException("Failed to parse recipe YAML.", null, ex);
        }

        if (document is null)
        {
            throw new RecipeLoadException("Recipe YAML content is empty or invalid.");
        }

        RunewireRecipe recipe = RecipeDocumentMapper.MapToDomain(document);

        RecipeValidationResult validationResult = _validator.Validate(recipe);
        if (!validationResult.IsValid)
        {
            throw new RecipeLoadException("Recipe failed validation.", validationResult.Errors);
        }

        return recipe;
    }

    /// <summary>
    /// Read the YAML file and then delegate to LoadFromString so the core logic stays in one place.
    /// </summary>
    public RunewireRecipe LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be null or whitespace.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Recipe file not found.", path);
        }

        string yaml;
        try
        {
            yaml = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            throw new RecipeLoadException($"Failed to read recipe file '{path}'.", null, ex);
        }

        return LoadFromString(yaml);
    }
}
