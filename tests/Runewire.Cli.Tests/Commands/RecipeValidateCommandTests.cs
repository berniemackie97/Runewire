namespace Runewire.Cli.Tests.Commands;

/// <summary>
/// End-to-end tests for the <c>validate</c> CLI command.
/// </summary>
public sealed class RecipeValidateCommandTests
{
    [Fact]
    public async Task Validate_valid_recipe_returns_exit_code_0_and_success_output()
    {
        // Setup
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-validate-test",
            """
            name: demo-recipe
            description: Demo injection into explorer
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
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("validate", recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Recipe is valid", output);
        Assert.Contains("demo-recipe", output);
    }

    [Fact]
    public async Task Validate_invalid_recipe_returns_exit_code_1_and_lists_errors()
    {
        // Setup
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-validate-invalid-test",
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
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("validate", recipePath);

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
    public async Task Validate_missing_file_returns_exit_code_2_and_error_message()
    {
        // Setup
        string recipePath = Path.Combine(Path.GetTempPath(), $"runewire-validate-missing-{Guid.NewGuid():N}.yaml");

        // Run
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("validate", recipePath);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("Recipe file not found", output);
    }

    [Fact]
    public async Task No_arguments_shows_help_and_returns_non_zero()
    {
        // Run
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync();

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Required command was not provided", output);
        Assert.Contains("Runewire process injection lab CLI", output);
        Assert.Contains("validate <recipe>", output);
    }
}
