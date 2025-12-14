namespace Runewire.Domain.Validation;

/// <summary>
/// Validation error.
/// </summary>
public sealed record RecipeValidationError(string Code, string Message);
