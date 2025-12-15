namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Serialized recipe shape as it comes off disk (YAML).
/// Loader maps this into RunewireRecipe after validation.
/// </summary>
internal sealed class RecipeDocument
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public RecipeTargetDocument? Target { get; set; }

    public RecipeTechniqueDocument? Technique { get; set; }

    public RecipePayloadDocument? Payload { get; set; }

    public RecipeSafetyDocument? Safety { get; set; }

    public List<RecipeStepDocument>? Steps { get; set; }
}
