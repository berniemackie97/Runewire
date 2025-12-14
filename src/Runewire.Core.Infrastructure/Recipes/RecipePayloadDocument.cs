namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Raw payload section as it comes from YAML.
/// Loader maps this into the domain recipe after validation.
/// </summary>
internal sealed class RecipePayloadDocument
{
    /// <summary>
    /// Payload path from the recipe (dll, shellcode blob, whatever the recipe points at).
    /// </summary>
    public string? Path { get; set; }
}
