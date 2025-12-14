using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Injection request produced from a recipe.
/// This is what the engine actually executes.
/// </summary>
public sealed record InjectionRequest(
    string RecipeName,
    string? RecipeDescription,
    RecipeTarget Target,
    string TechniqueName,
    IReadOnlyDictionary<string, string>? TechniqueParameters,
    string PayloadPath,
    bool AllowKernelDrivers,
    bool RequireInteractiveConsent
);
