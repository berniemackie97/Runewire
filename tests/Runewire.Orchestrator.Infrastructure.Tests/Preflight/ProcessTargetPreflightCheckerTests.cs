using System.Diagnostics;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Infrastructure.Preflight;

namespace Runewire.Orchestrator.Infrastructure.Tests.Preflight;

public sealed class ProcessTargetPreflightCheckerTests
{
    [Fact]
    public void Check_succeeds_for_current_process_name_even_with_exe_suffix()
    {
        // Arrange
        using Process current = Process.GetCurrentProcess();
        string nameWithExe = $"{current.ProcessName}.exe";

        RunewireRecipe recipe = new(
            Name: "test",
            Description: null,
            Target: RecipeTarget.ForProcessName(nameWithExe),
            Technique: new InjectionTechnique("CreateRemoteThread"),
            PayloadPath: "C:\\dummy",
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false);

        ProcessTargetPreflightChecker checker = new();

        // Act
        TargetPreflightResult result = checker.Check(recipe);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Check_fails_when_pid_not_found()
    {
        // Arrange
        RunewireRecipe recipe = new(
            Name: "test",
            Description: null,
            Target: RecipeTarget.ForProcessId(int.MaxValue),
            Technique: new InjectionTechnique("CreateRemoteThread"),
            PayloadPath: "C:\\dummy",
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false);

        ProcessTargetPreflightChecker checker = new();

        // Act
        TargetPreflightResult result = checker.Check(recipe);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "TARGET_PID_NOT_FOUND");
    }

    [Fact]
    public void Check_fails_when_process_name_not_found()
    {
        // Arrange
        string bogusName = $"runewire-missing-{Guid.NewGuid():N}";

        RunewireRecipe recipe = new(
            Name: "test",
            Description: null,
            Target: RecipeTarget.ForProcessName(bogusName),
            Technique: new InjectionTechnique("CreateRemoteThread"),
            PayloadPath: "C:\\dummy",
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false);

        ProcessTargetPreflightChecker checker = new();

        // Act
        TargetPreflightResult result = checker.Check(recipe);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "TARGET_NAME_NOT_FOUND");
    }
}
