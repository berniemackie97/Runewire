namespace Runewire.Domain.Recipes;

/// <summary>
/// A single step in a recipe workflow.
/// </summary>
public sealed record RecipeStep
{
    public RecipeStepKind Kind { get; init; }

    public string? TechniqueName { get; init; }

    public IReadOnlyDictionary<string, string>? TechniqueParameters { get; init; }

    public string? PayloadPath { get; init; }

    public int? WaitMilliseconds { get; init; }

    public WaitCondition? Condition { get; init; }

    public RecipeStep(RecipeStepKind kind)
    {
        Kind = kind;
    }

    public static RecipeStep Inject(string techniqueName, string payloadPath, IReadOnlyDictionary<string, string>? parameters = null) =>
        new(RecipeStepKind.InjectTechnique)
        {
            TechniqueName = techniqueName,
            PayloadPath = payloadPath,
            TechniqueParameters = parameters
        };

    public static RecipeStep Wait(int milliseconds) =>
        new(RecipeStepKind.Wait)
        {
            WaitMilliseconds = milliseconds
        };

    public static RecipeStep Suspend() => new(RecipeStepKind.Suspend);

    public static RecipeStep Resume() => new(RecipeStepKind.Resume);

    public static RecipeStep WaitFor(WaitCondition condition) =>
        new(RecipeStepKind.Wait)
        {
            Condition = condition
        };
}
