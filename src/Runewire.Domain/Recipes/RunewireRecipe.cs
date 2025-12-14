namespace Runewire.Domain.Recipes;

/// <summary>
/// injection run described by a recipe.
/// </summary>
public sealed record RunewireRecipe(
    string Name,
    string? Description,
    RecipeTarget Target,
    InjectionTechnique Technique,
    string PayloadPath,
    bool RequireInteractiveConsent,
    bool AllowKernelDrivers
);
