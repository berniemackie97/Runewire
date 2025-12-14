namespace Runewire.Domain.Validation;

/// <summary>
/// Result of validating a recipe.
/// Either IsValid is true and Errors is empty, or IsValid is false and Errors explains why.
/// </summary>
public sealed record RecipeValidationResult(bool IsValid, IReadOnlyList<RecipeValidationError> Errors)
{
    /// <summary>
    /// Success, no errors.
    /// </summary>
    public static RecipeValidationResult Success() => new(true, []);

    /// <summary>
    /// Failure with errors (null becomes empty).
    /// </summary>
    public static RecipeValidationResult Failure(IEnumerable<RecipeValidationError> errors)
    {
        List<RecipeValidationError> list = errors?.ToList() ?? [];
        return new(false, list);
    }
}
