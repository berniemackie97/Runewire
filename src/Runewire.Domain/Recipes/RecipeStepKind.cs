namespace Runewire.Domain.Recipes;

/// <summary>
/// Types of steps in a recipe workflow.
/// </summary>
public enum RecipeStepKind
{
    InjectTechnique = 0,
    Wait = 1,
    Suspend = 2,
    Resume = 3,
}
