using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

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

        // Registry delegate that says "no technique is known".
        BasicRecipeValidator validator = new(_ => false);

        // Act
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_UNKNOWN");
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
        BasicRecipeValidator validator = new(_ => false);

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
}
