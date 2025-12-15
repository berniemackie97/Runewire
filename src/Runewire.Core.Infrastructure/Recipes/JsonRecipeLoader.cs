using System.Text.Json;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Loads JSON into a RunewireRecipe, then validates it.
/// </summary>
public sealed class JsonRecipeLoader(IRecipeValidator validator) : IJsonRecipeLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly IRecipeValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    public RunewireRecipe LoadFromString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        RecipeDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<RecipeDocument>(text, SerializerOptions);
        }
        catch (Exception ex)
        {
            throw new RecipeLoadException("Failed to parse recipe JSON.", null, ex);
        }

        if (document is null)
        {
            throw new RecipeLoadException("Recipe JSON content is empty or invalid.");
        }

        RunewireRecipe recipe = RecipeDocumentMapper.MapToDomain(document);

        RecipeValidationPipeline.Validate(recipe, _validator);
        return recipe;
    }

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

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            throw new RecipeLoadException($"Failed to read recipe file '{path}'.", null, ex);
        }

        RecipeDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<RecipeDocument>(json, SerializerOptions);
        }
        catch (Exception ex)
        {
            throw new RecipeLoadException("Failed to parse recipe JSON.", null, ex);
        }

        if (document is null)
        {
            throw new RecipeLoadException("Recipe JSON content is empty or invalid.");
        }

        string? baseDir = Path.GetDirectoryName(Path.GetFullPath(path));
        RunewireRecipe recipe = RecipeDocumentMapper.MapToDomain(document, baseDir);
        RecipeValidationPipeline.Validate(recipe, _validator);
        return recipe;
    }
}
