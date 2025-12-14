using Runewire.Domain.Recipes;

namespace Runewire.Domain.Validation;

/// <summary>
/// Validates a RunewireRecipe and returns a RecipeValidationResult.
/// </summary>
public interface IRecipeValidator
{
    /// <summary>
    /// Validate the recipe and return the result.
    /// </summary>
    RecipeValidationResult Validate(RunewireRecipe recipe);
}
