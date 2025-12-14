using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Shared validation pipeline for recipe loaders.
/// Runs semantic validation, then applies infrastructure-only checks (like payload existence).
/// </summary>
internal static class RecipeValidationPipeline
{
    public static void Validate(RunewireRecipe recipe, IRecipeValidator validator)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(validator);

        RecipeValidationResult validationResult = validator.Validate(recipe);
        if (!validationResult.IsValid)
        {
            throw new RecipeLoadException("Recipe failed validation.", validationResult.Errors);
        }

        EnsurePayloadExists(recipe);
    }

    private static void EnsurePayloadExists(RunewireRecipe recipe)
    {
        // Basic recipe validator already checked for non-empty path. This is the I/O check.
        if (!File.Exists(recipe.PayloadPath))
        {
            RecipeValidationError error = new("PAYLOAD_PATH_NOT_FOUND", $"Payload file not found: {recipe.PayloadPath}");
            throw new RecipeLoadException("Recipe failed validation.", [error]);
        }
    }
}
