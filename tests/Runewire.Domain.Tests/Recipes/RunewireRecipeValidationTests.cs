using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using Runewire.Domain.Techniques;

namespace Runewire.Domain.Tests.Recipes;

/// <summary>
/// Unit tests for <see cref="BasicRecipeValidator"/> and the core
/// <see cref="RunewireRecipe"/> validation rules.
/// </summary>
public class RunewireRecipeValidationTests
{
    private static RunewireRecipe CreateValidRecipe()
    {
        return new RunewireRecipe(
            Name: "demo-recipe",
            Description: "A minimal, valid test recipe.",
            Target: RecipeTarget.ForProcessName("explorer.exe"),
            Technique: new InjectionTechnique("CreateRemoteThread"),
            PayloadPath: @"C:\lab\payloads\demo.dll",
            RequireInteractiveConsent: true,
            AllowKernelDrivers: false
        );
    }

    [Fact]
    public void Valid_recipe_passes_validation()
    {
        // Arrange
        RunewireRecipe recipe = CreateValidRecipe();
        BasicRecipeValidator validator = new();

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_null_recipe_throws()
    {
        // Arrange
        BasicRecipeValidator validator = new();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    [Fact]
    public void Missing_name_fails_validation()
    {
        // Arrange
        RunewireRecipe recipe = CreateValidRecipe() with { Name = "   " };
        BasicRecipeValidator validator = new();

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_NAME_REQUIRED");
    }

    [Fact]
    public void Overly_long_name_fails_validation()
    {
        // Arrange
        string longName = new('x', 101);
        RunewireRecipe recipe = CreateValidRecipe() with { Name = longName };
        BasicRecipeValidator validator = new();

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_NAME_TOO_LONG");
    }

    [Fact]
    public void Missing_payload_path_fails_validation()
    {
        // Arrange
        RunewireRecipe recipe = CreateValidRecipe() with { PayloadPath = "" };
        BasicRecipeValidator validator = new();

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PAYLOAD_PATH_REQUIRED");
    }

    [Fact]
    public void Missing_technique_name_fails_validation()
    {
        // Arrange
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Technique = new InjectionTechnique("  ")
        };
        BasicRecipeValidator validator = new();

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_NAME_REQUIRED");
    }

    [Fact]
    public void Process_by_id_requires_positive_pid()
    {
        // Arrange
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Target = RecipeTarget.ForProcessId(0)
        };
        BasicRecipeValidator validator = new();

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TARGET_PID_INVALID");
    }

    [Fact]
    public void Process_by_name_requires_non_empty_name()
    {
        // Arrange
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Target = RecipeTarget.ForProcessName("   ")
        };
        BasicRecipeValidator validator = new();

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TARGET_NAME_REQUIRED");
    }

    [Fact]
    public void Launch_process_requires_path()
    {
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Target = RecipeTarget.ForLaunchProcess("")
        };
        BasicRecipeValidator validator = new();

        RecipeValidationResult result = validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TARGET_LAUNCH_PATH_REQUIRED");
    }

    [Fact]
    public void Steps_inject_requires_technique_and_payload()
    {
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Steps = new List<RecipeStep> { RecipeStep.Inject("", "") }
        };
        BasicRecipeValidator validator = new();

        RecipeValidationResult result = validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "STEP_TECHNIQUE_NAME_REQUIRED");
        Assert.Contains(result.Errors, e => e.Code == "STEP_PAYLOAD_PATH_REQUIRED");
    }

    [Fact]
    public void Steps_wait_requires_duration()
    {
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Steps = new List<RecipeStep> { RecipeStep.Wait(0) }
        };
        BasicRecipeValidator validator = new();

        RecipeValidationResult result = validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "STEP_WAIT_REQUIRED");
    }

    [Fact]
    public void Steps_inject_requires_registered_technique_when_registry_is_available()
    {
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Steps = new List<RecipeStep> { RecipeStep.Inject("UnknownStepTech", @"C:\payloads\step.dll") }
        };

        FakeRegistry registry = new([]);
        BasicRecipeValidator validator = new(registry);

        RecipeValidationResult result = validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "STEP_TECHNIQUE_UNKNOWN");
    }

    [Fact]
    public void Steps_inject_enforces_required_parameters()
    {
        InjectionTechniqueDescriptor technique = new(
            InjectionTechniqueId.Unknown,
            name: "StepTech",
            displayName: "Step tech",
            category: "User-mode",
            description: "Test step technique",
            requiresKernelMode: false,
            platforms: new[] { TechniquePlatform.Windows },
            parameters: new[] { new TechniqueParameter("param", "Required param") });

        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Steps = new List<RecipeStep> { RecipeStep.Inject(technique.Name, @"C:\payloads\step.dll", new Dictionary<string, string> { { "param", "" } }) }
        };

        FakeRegistry registry = new([technique]);
        BasicRecipeValidator validator = new(registry);

        RecipeValidationResult result = validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "STEP_TECHNIQUE_PARAM_REQUIRED");
    }

    [Fact]
    public void Allowing_kernel_drivers_requires_interactive_consent()
    {
        // Arrange
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            AllowKernelDrivers = true,
            RequireInteractiveConsent = false,
        };
        BasicRecipeValidator validator = new();

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "SAFETY_KERNEL_DRIVER_CONSENT_REQUIRED");
    }

    [Fact]
    public void Unknown_technique_fails_when_registry_reports_it_missing()
    {
        // Arrange: technique name that our fake "registry" will reject.
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Technique = new InjectionTechnique("TotallyFakeTechnique")
        };

        FakeRegistry registry = new([]);
        BasicRecipeValidator validator = new(registry);

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_UNKNOWN");
    }

    [Fact]
    public void Technique_requiring_kernel_mode_fails_when_kernel_not_allowed()
    {
        // Arrange
        InjectionTechniqueDescriptor kernelTechnique = new(
            InjectionTechniqueId.Unknown,
            name: "KernelOnly",
            displayName: "Kernel only",
            category: "Kernel",
            description: "Kernel-only test",
            requiresKernelMode: true,
            platforms: new[] { TechniquePlatform.Windows },
            parameters: Array.Empty<TechniqueParameter>());

        FakeRegistry registry = new([kernelTechnique]);

        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Technique = new InjectionTechnique(kernelTechnique.Name),
            AllowKernelDrivers = false,
            RequireInteractiveConsent = true
        };

        BasicRecipeValidator validator = new(registry);

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_KERNEL_MODE_REQUIRED");
    }

    [Fact]
    public void Technique_required_parameters_must_be_present()
    {
        // Arrange
        InjectionTechniqueDescriptor technique = new(
            InjectionTechniqueId.Unknown,
            name: "RequiresParam",
            displayName: "Requires param",
            category: "User-mode",
            description: "Needs param",
            requiresKernelMode: false,
            platforms: new[] { TechniquePlatform.Windows },
            parameters: new[] { new TechniqueParameter("dllPath", "DLL path parameter") });

        FakeRegistry registry = new([technique]);

        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Technique = new InjectionTechnique(technique.Name, Parameters: new Dictionary<string, string> { { "dllPath", "" } })
        };

        BasicRecipeValidator validator = new(registry);

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_PARAM_REQUIRED");
    }

    [Fact]
    public void Multiple_violations_are_reported_together()
    {
        // Arrange: break several rules at once.
        RunewireRecipe recipe = new(
            Name: "   ",
            Description: null,
            Target: RecipeTarget.ForProcessId(0),
            Technique: new InjectionTechnique("  "),
            PayloadPath: "",
            RequireInteractiveConsent: false,
            AllowKernelDrivers: true);

        // Use a registry that rejects everything, in case the technique
        // name sneaks past the whitespace check in the future.
        BasicRecipeValidator validator = new(new FakeRegistry([]));

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);

        // We expect at least one error from each logical validation area.
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_NAME_REQUIRED");
        Assert.Contains(result.Errors, e => e.Code == "TARGET_PID_INVALID");
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_NAME_REQUIRED");
        Assert.Contains(result.Errors, e => e.Code == "PAYLOAD_PATH_REQUIRED");
        Assert.Contains(result.Errors, e => e.Code == "SAFETY_KERNEL_DRIVER_CONSENT_REQUIRED");
    }

    [Fact]
    public void Technique_requiring_driver_fails_when_kernel_drivers_not_allowed()
    {
        InjectionTechniqueDescriptor technique = new(
            InjectionTechniqueId.Unknown,
            name: "NeedsDriver",
            displayName: "Needs driver",
            category: "Kernel",
            description: "Driver dependent technique",
            requiresKernelMode: false,
            platforms: new[] { TechniquePlatform.Windows },
            parameters: Array.Empty<TechniqueParameter>(),
            implemented: true,
            requiresDriver: true);

        FakeRegistry registry = new([technique]);

        RunewireRecipe recipe = CreateValidRecipe() with
        {
            AllowKernelDrivers = false,
            Technique = new InjectionTechnique(technique.Name),
        };

        BasicRecipeValidator validator = new(registry);

        RecipeValidationResult result = validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_DRIVER_REQUIRED");
    }

    [Fact]
    public void Steps_wait_condition_requires_value()
    {
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Steps = new List<RecipeStep> { RecipeStep.WaitFor(new WaitCondition(WaitConditionKind.ModuleLoaded, "")) }
        };
        BasicRecipeValidator validator = new();

        RecipeValidationResult result = validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "STEP_WAIT_CONDITION_VALUE_REQUIRED");
    }

    [Fact]
    public void Steps_wait_cannot_have_both_duration_and_condition()
    {
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Steps = new List<RecipeStep>
            {
                new RecipeStep(RecipeStepKind.Wait){ WaitMilliseconds = 100, Condition = new WaitCondition(WaitConditionKind.FileExists, @"C:\temp\flag") }
            }
        };
        BasicRecipeValidator validator = new();

        RecipeValidationResult result = validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "STEP_WAIT_AMBIGUOUS");
    }

    private sealed class FakeRegistry(IReadOnlyList<InjectionTechniqueDescriptor> techniques) : IInjectionTechniqueRegistry
    {
        public IEnumerable<InjectionTechniqueDescriptor> GetAll() => techniques;

        public InjectionTechniqueDescriptor? GetById(InjectionTechniqueId id) => techniques.FirstOrDefault(t => t.Id == id);

        public InjectionTechniqueDescriptor? GetByName(string name) => techniques.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
