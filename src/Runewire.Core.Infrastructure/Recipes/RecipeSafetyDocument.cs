namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Raw safety section from YAML.
/// This is the serialized shape, not the domain model.
/// </summary>
internal sealed class RecipeSafetyDocument
{
    /// <summary>
    /// If the recipe does not say, I default this to true.
    /// Safer by default, and it forces me to be intentional when running sketchy stuff.
    /// </summary>
    public bool RequireInteractiveConsent { get; set; } = true;

    /// <summary>
    /// Whether this recipe is allowed to involve kernel drivers.
    /// </summary>
    public bool AllowKernelDrivers { get; set; }
}
