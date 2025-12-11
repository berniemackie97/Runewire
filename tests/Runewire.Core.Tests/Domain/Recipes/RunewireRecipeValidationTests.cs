using Runewire.Core.Domain.Recipes;
using Runewire.Core.Domain.Validation;

namespace Runewire.Core.Tests.Domain.Recipes;

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
        // Setup
        RunewireRecipe recipe = CreateValidRecipe();
        BasicRecipeValidator validator = new();

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_name_fails_validation()
    {
        // Setup
        RunewireRecipe recipe = CreateValidRecipe() with { Name = "   " };
        BasicRecipeValidator validator = new();

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_NAME_REQUIRED");
    }

    [Fact]
    public void Overly_long_name_fails_validation()
    {
        // Setup
        string longName = new('x', 101);
        RunewireRecipe recipe = CreateValidRecipe() with { Name = longName };
        BasicRecipeValidator validator = new();

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "RECIPE_NAME_TOO_LONG");
    }

    [Fact]
    public void Missing_payload_path_fails_validation()
    {
        // Setup
        RunewireRecipe recipe = CreateValidRecipe() with { PayloadPath = "" };
        BasicRecipeValidator validator = new();

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "PAYLOAD_PATH_REQUIRED");
    }

    [Fact]
    public void Missing_technique_name_fails_validation()
    {
        // Setup
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Technique = new InjectionTechnique("  ")
        };
        BasicRecipeValidator validator = new();

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_NAME_REQUIRED");
    }

    [Fact]
    public void Process_by_id_requires_positive_pid()
    {
        // Setup
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Target = RecipeTarget.ForProcessId(0)
        };
        BasicRecipeValidator validator = new();

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TARGET_PID_INVALID");
    }

    [Fact]
    public void Process_by_name_requires_non_empty_name()
    {
        // Setup
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Target = RecipeTarget.ForProcessName("   ")
        };
        BasicRecipeValidator validator = new();

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TARGET_NAME_REQUIRED");
    }

    [Fact]
    public void Allowing_kernel_drivers_requires_interactive_consent()
    {
        // Setup
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            AllowKernelDrivers = true,
            RequireInteractiveConsent = false,
        };
        BasicRecipeValidator validator = new();

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.Code == "SAFETY_KERNEL_DRIVER_CONSENT_REQUIRED"
        );
    }

    [Fact]
    public void Unknown_technique_fails_when_registry_reports_it_missing()
    {
        // Setup: technique name that our fake "registry" will reject.
        RunewireRecipe recipe = CreateValidRecipe() with
        {
            Technique = new InjectionTechnique("TotallyFakeTechnique")
        };

        // Registry delegate that says "no technique is known".
        BasicRecipeValidator validator = new(_ => false);

        // Run
        RecipeValidationResult result = validator.Validate(recipe);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TECHNIQUE_UNKNOWN");
    }
}
