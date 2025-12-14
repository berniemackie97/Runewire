using Runewire.Domain.Validation;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Recipe failed to load from YAML.
///
/// This covers 2 buckets:
/// 1) structural issues (IO, YAML parse, etc)
/// 2) semantic validation issues (ValidationErrors is populated)
///
/// How I meant for it to be used:
/// if ValidationErrors has items, the YAML loaded but the recipe is not valid.
/// if ValidationErrors is empty, something broke before we even got that far.
/// </summary>
public sealed class RecipeLoadException(string message, IReadOnlyList<RecipeValidationError>? validationErrors = null, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Semantic validation errors. Empty means this was a structural load failure.
    /// </summary>
    public IReadOnlyList<RecipeValidationError> ValidationErrors { get; } = validationErrors ?? [];
}
