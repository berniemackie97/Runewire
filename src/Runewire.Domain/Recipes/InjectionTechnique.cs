namespace Runewire.Domain.Recipes;

/// <summary>
/// The technique name from the recipe.
/// This is just the logical name (like CreateRemoteThread).
/// </summary>
public sealed record InjectionTechnique(string Name, IReadOnlyDictionary<string, string>? Parameters = null);
