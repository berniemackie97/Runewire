namespace Runewire.Cli.Tests.Commands;

/// <summary>
/// End-to-end tests for the <c>run</c> CLI command executing recipes.
/// </summary>
public sealed class RecipeRunCommandTests
{
    [Fact]
    public async Task Run_valid_recipe_returns_exit_code_0_and_reports_success()
    {
        // Setup
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-run-test",
            """
            name: demo-run
            description: Demo run execution.
            target:
              kind: processByName
              processName: explorer.exe
            technique:
              name: CreateRemoteThread
            payload:
              path: C:\lab\payloads\demo.dll
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """
        );

        // Run
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("run", recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Injection succeeded", output);
        Assert.Contains("demo-run", output);
    }

    [Fact]
    public async Task Run_invalid_recipe_returns_exit_code_1_and_lists_errors()
    {
        // Setup
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-run-invalid-test",
            """
            name: ''
            target:
              kind: processById
              processId: 0
            technique:
              name: ''
            payload:
              path: ''
            safety:
              allowKernelDrivers: true
              requireInteractiveConsent: false
            """
        );

        // Run
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("run", recipePath);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Recipe is invalid", output);
        Assert.Contains("RECIPE_NAME_REQUIRED", output);
        Assert.Contains("TARGET_PID_INVALID", output);
        Assert.Contains("TECHNIQUE_NAME_REQUIRED", output);
        Assert.Contains("PAYLOAD_PATH_REQUIRED", output);
        Assert.Contains("SAFETY_KERNEL_DRIVER_CONSENT_REQUIRED", output);
    }

    [Fact]
    public async Task Run_missing_file_returns_exit_code_2_and_error_message()
    {
        // Setup
        string recipePath = Path.Combine(Path.GetTempPath(), $"runewire-run-missing-{Guid.NewGuid():N}.yaml");

        // Run
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("run", recipePath);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("Recipe file not found", output);
    }
}
