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
        ValidateSteps(recipe, errors);
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

            case RecipeTargetKind.LaunchProcess:
                if (string.IsNullOrWhiteSpace(recipe.Target.LaunchPath))
                {
                    errors.Add(new RecipeValidationError("TARGET_LAUNCH_PATH_REQUIRED", "Launch path is required when launching a process."));
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

    private void ValidateSteps(RunewireRecipe recipe, List<RecipeValidationError> errors)
    {
        if (recipe.Steps is null || recipe.Steps.Count == 0)
        {
            return;
        }

        int index = 0;
        foreach (RecipeStep step in recipe.Steps)
        {
            switch (step.Kind)
            {
                case RecipeStepKind.InjectTechnique:
                    if (string.IsNullOrWhiteSpace(step.TechniqueName))
                    {
                        errors.Add(new RecipeValidationError("STEP_TECHNIQUE_NAME_REQUIRED", $"Step {index} requires a technique name."));
                    }
                    if (string.IsNullOrWhiteSpace(step.PayloadPath))
                    {
                        errors.Add(new RecipeValidationError("STEP_PAYLOAD_PATH_REQUIRED", $"Step {index} requires a payload path."));
                    }
                    else
                    {
                        ValidateStepTechnique(recipe, step, errors);
                    }
                    break;

                case RecipeStepKind.Wait:
                    bool hasDelay = step.WaitMilliseconds is not null && step.WaitMilliseconds > 0;
                    bool hasCondition = step.Condition is not null;

                    if (!hasDelay && !hasCondition)
                    {
                        errors.Add(new RecipeValidationError("STEP_WAIT_REQUIRED", $"Step {index} requires a wait duration or condition."));
                    }

                    if (hasDelay && hasCondition)
                    {
                        errors.Add(new RecipeValidationError("STEP_WAIT_AMBIGUOUS", $"Step {index} cannot have both a wait duration and a condition."));
                    }

                    if (hasCondition)
                    {
                        ValidateWaitCondition(step.Condition!, index, errors);
                    }
                    else if (!hasDelay)
                    {
                        errors.Add(new RecipeValidationError("STEP_WAIT_DURATION_REQUIRED", $"Step {index} requires a positive wait duration."));
                    }
                    break;

                case RecipeStepKind.Suspend:
                case RecipeStepKind.Resume:
                    // Valid kinds, nothing else to validate right now.
                    break;

                default:
                    errors.Add(new RecipeValidationError("STEP_KIND_UNKNOWN", $"Step {index} has unknown kind '{step.Kind}'."));
                    break;
            }

            index++;
        }
    }

    private void ValidateStepTechnique(RunewireRecipe recipe, RecipeStep step, List<RecipeValidationError> errors)
    {
        if (_techniqueRegistry is null || string.IsNullOrWhiteSpace(step.TechniqueName))
        {
            return;
        }

        InjectionTechniqueDescriptor? descriptor = _techniqueRegistry.GetByName(step.TechniqueName);
        if (descriptor is null)
        {
            errors.Add(new RecipeValidationError("STEP_TECHNIQUE_UNKNOWN", $"Step technique '{step.TechniqueName}' is not registered."));
            return;
        }

        if (descriptor.RequiresKernelMode && !recipe.AllowKernelDrivers)
        {
            errors.Add(new RecipeValidationError("STEP_TECHNIQUE_KERNEL_MODE_REQUIRED", $"Step technique '{step.TechniqueName}' requires kernel driver support."));
        }

        if (descriptor.RequiresDriver && !recipe.AllowKernelDrivers)
        {
            errors.Add(new RecipeValidationError("STEP_TECHNIQUE_DRIVER_REQUIRED", $"Step technique '{step.TechniqueName}' requires kernel driver support."));
        }

        foreach (TechniqueParameter parameter in descriptor.Parameters.Where(p => p.Required))
        {
            if (step.TechniqueParameters is null || !step.TechniqueParameters.TryGetValue(parameter.Name, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new RecipeValidationError("STEP_TECHNIQUE_PARAM_REQUIRED", $"Step technique '{step.TechniqueName}' requires parameter '{parameter.Name}'."));
            }
        }
    }

    private static void ValidateWaitCondition(WaitCondition condition, int index, List<RecipeValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            errors.Add(new RecipeValidationError("STEP_WAIT_CONDITION_VALUE_REQUIRED", $"Step {index} requires a condition value."));
        }

        if (condition.TimeoutMilliseconds is not null && condition.TimeoutMilliseconds <= 0)
        {
            errors.Add(new RecipeValidationError("STEP_WAIT_CONDITION_TIMEOUT_INVALID", $"Step {index} condition timeout must be positive."));
        }

        switch (condition.Kind)
        {
            case WaitConditionKind.ModuleLoaded:
            case WaitConditionKind.FileExists:
            case WaitConditionKind.ProcessExited:
            case WaitConditionKind.WindowClass:
            case WaitConditionKind.WindowTitle:
            case WaitConditionKind.NamedPipeAvailable:
            case WaitConditionKind.ProcessHandleReady:
            case WaitConditionKind.TcpPortListening:
            case WaitConditionKind.NamedEvent:
            case WaitConditionKind.NamedMutex:
            case WaitConditionKind.NamedSemaphore:
            case WaitConditionKind.SharedMemoryExists:
            case WaitConditionKind.RegistryValueEquals:
            case WaitConditionKind.ChildProcessAppeared:
            case WaitConditionKind.HttpReachable:
            case WaitConditionKind.ServiceState:
            case WaitConditionKind.EnvironmentVariableEquals:
            case WaitConditionKind.FileContentContains:
                break;
            default:
                errors.Add(new RecipeValidationError("STEP_WAIT_CONDITION_UNKNOWN", $"Step {index} has unknown wait condition '{condition.Kind}'."));
                break;
        }
    }
}
