using Runewire.Core.Infrastructure.Techniques;
using Runewire.Domain.Validation;

namespace Runewire.Core.Infrastructure.Validation;

/// <summary>
/// Builds the standard recipe validators.
/// I keep this centralized so every frontend validates the same way.
/// </summary>
public static class RecipeValidatorFactory
{
    // Built in registry is immutable, so I reuse a single instance.
    private static readonly BuiltInInjectionTechniqueRegistry TechniqueRegistry = new();

    /// <summary>
    /// Default validator wired up to the built in technique registry.
    /// </summary>
    public static BasicRecipeValidator CreateDefaultValidator()
    {
        return new BasicRecipeValidator(techniqueName => TechniqueRegistry.GetByName(techniqueName) is not null);
    }
}
