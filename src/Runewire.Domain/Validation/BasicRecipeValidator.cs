using Runewire.Domain.Recipes;
using Runewire.Domain.Techniques;

namespace Runewire.Domain.Validation;

/// <summary>
/// Default recipe validator.
/// </summary>
public sealed class BasicRecipeValidator(IInjectionTechniqueRegistry? techniqueRegistry = null) : IRecipeValidator
{
    private readonly IInjectionTechniqueRegistry? _techniqueRegistry = techniqueRegistry;

    /// <summary>
    /// Validate a recipe and return all errors found.
    /// </summary>
    public RecipeValidationResult Validate(RunewireRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        List<RecipeValidationError> errors = [];

        ValidateName(recipe, errors);
        ValidatePayloadPath(recipe, errors);
        ValidateTechnique(recipe, errors);
        ValidateTarget(recipe, errors);
        ValidateSafety(recipe, errors);

        return errors.Count == 0 ? RecipeValidationResult.Success() : RecipeValidationResult.Failure(errors);
    }

    private static void ValidateName(RunewireRecipe recipe, List<RecipeValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            errors.Add(new RecipeValidationError("RECIPE_NAME_REQUIRED", "Recipe name is required."));
            return;
        }

        const int maxLength = 100;
        if (recipe.Name.Length > maxLength)
        {
            errors.Add(new RecipeValidationError("RECIPE_NAME_TOO_LONG", $"Recipe name must be at most {maxLength} characters."));
        }
    }

    private static void ValidatePayloadPath(RunewireRecipe recipe, List<RecipeValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(recipe.PayloadPath))
        {
            errors.Add(new RecipeValidationError("PAYLOAD_PATH_REQUIRED", "Payload path is required."));
        }
    }

    private void ValidateTechnique(RunewireRecipe recipe, List<RecipeValidationError> errors)
    {
        string name = recipe.Technique.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new RecipeValidationError("TECHNIQUE_NAME_REQUIRED", "Injection technique name is required."));
            return;
        }

        if (_techniqueRegistry is null)
        {
            return;
        }

        InjectionTechniqueDescriptor? descriptor = _techniqueRegistry.GetByName(name);
        if (descriptor is null)
        {
            errors.Add(new RecipeValidationError("TECHNIQUE_UNKNOWN", $"Injection technique '{name}' is not registered."));
            return;
        }

        if (descriptor.RequiresKernelMode && !recipe.AllowKernelDrivers)
        {
            errors.Add(new RecipeValidationError("TECHNIQUE_KERNEL_MODE_REQUIRED", $"Technique '{name}' requires kernel driver support."));
        }

        if (descriptor.RequiresDriver && !recipe.AllowKernelDrivers)
        {
            errors.Add(new RecipeValidationError("TECHNIQUE_DRIVER_REQUIRED", $"Technique '{name}' requires kernel driver support."));
        }

        foreach (TechniqueParameter parameter in descriptor.Parameters.Where(p => p.Required))
        {
            if (!HasTechniqueParameter(recipe.Technique, parameter.Name))
            {
                errors.Add(new RecipeValidationError("TECHNIQUE_PARAM_REQUIRED", $"Technique '{name}' requires parameter '{parameter.Name}'."));
            }
        }
    }

    private static void ValidateTarget(RunewireRecipe recipe, List<RecipeValidationError> errors)
    {
        switch (recipe.Target.Kind)
        {
            case RecipeTargetKind.Self:
                break;

            case RecipeTargetKind.ProcessById:
                if (recipe.Target.ProcessId is null || recipe.Target.ProcessId <= 0)
                {
                    errors.Add(new RecipeValidationError("TARGET_PID_INVALID", "Process ID must be a positive integer when targeting by ID."));
                }
                break;

            case RecipeTargetKind.ProcessByName:
                if (string.IsNullOrWhiteSpace(recipe.Target.ProcessName))
                {
                    errors.Add(new RecipeValidationError("TARGET_NAME_REQUIRED", "Process name is required when targeting by name."));
                }
                break;

            default:
                errors.Add(new RecipeValidationError("TARGET_KIND_UNKNOWN", $"Unknown target kind '{recipe.Target.Kind}'."));
                break;
        }
    }

    private static void ValidateSafety(RunewireRecipe recipe, List<RecipeValidationError> errors)
    {
        // Guardrail: if kernel drivers are allowed, force interactive consent.
        if (recipe.AllowKernelDrivers && !recipe.RequireInteractiveConsent)
        {
            errors.Add(new RecipeValidationError("SAFETY_KERNEL_DRIVER_CONSENT_REQUIRED", "Recipes that allow kernel drivers must require interactive consent."));
        }
    }

    private static bool HasTechniqueParameter(InjectionTechnique technique, string parameterName)
    {
        if (technique.Parameters is null)
        {
            return false;
        }

        return technique.Parameters.TryGetValue(parameterName, out string? value) && !string.IsNullOrWhiteSpace(value);
    }
}
