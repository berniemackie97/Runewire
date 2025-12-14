namespace Runewire.Core.Infrastructure.Recipes;

internal sealed class RecipeStepDocument
{
    public string? Kind { get; set; }

    public string? TechniqueName { get; set; }

    public Dictionary<string, string>? TechniqueParameters { get; set; }

    public string? PayloadPath { get; set; }

    public int? WaitMilliseconds { get; set; }

    public WaitConditionDocument? Condition { get; set; }
}
