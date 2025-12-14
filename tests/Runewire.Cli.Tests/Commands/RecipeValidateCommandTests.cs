using Runewire.Cli.Commands;
using System.Diagnostics;

namespace Runewire.Cli.Tests.Commands;

/// <summary>
/// End to end tests for validate.
/// </summary>
public sealed class RecipeValidateCommandTests
{
    [Fact]
    public async Task Validate_valid_recipe_returns_exit_code_0_and_success_output()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string processName = Process.GetCurrentProcess().ProcessName;
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-validate-test",
            $"""
            name: demo-recipe
            description: Demo injection into explorer
            target:
              kind: processByName
              processName: {processName}
            technique:
              name: CreateRemoteThread
            payload:
              path: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """,
            extension: "yaml"
        );

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Recipe is valid", output);
        Assert.Contains("demo-recipe", output);
    }

    [Fact]
    public async Task Validate_invalid_recipe_returns_exit_code_1_and_lists_errors()
    {
        // Arrange
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

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, recipePath);

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
        // Arrange
        string recipePath = Path.Combine(Path.GetTempPath(), $"runewire-validate-missing-{Guid.NewGuid():N}.yaml");

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("Recipe file not found", output);
    }

    [Fact]
    public async Task Validate_missing_file_with_json_returns_structured_error()
    {
        // Arrange
        string recipePath = Path.Combine(Path.GetTempPath(), $"runewire-validate-missing-{Guid.NewGuid():N}.yaml");

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("\"status\": \"error\"", output);
        Assert.Contains("\"meta\"", output);
    }

    [Fact]
    public async Task No_arguments_shows_help_and_returns_non_zero()
    {
        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync();

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Required command was not provided", output);
        Assert.Contains("Runewire process injection lab CLI", output);
        Assert.Contains(RecipeValidateCommand.CommandName, output);
    }

    [Fact]
    public async Task Validate_json_recipe_returns_exit_code_0_and_success_output()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string processName = Process.GetCurrentProcess().ProcessName;
        string jsonTemplate = """
            {
              "name": "demo-recipe-json",
              "description": "Demo injection into explorer",
              "target": { "kind": "processByName", "processName": "__PROC__" },
              "technique": { "name": "CreateRemoteThread" },
              "payload": { "path": "__PAYLOAD__" },
              "safety": { "requireInteractiveConsent": true, "allowKernelDrivers": false }
            }
            """;
        string json = jsonTemplate
            .Replace("__PAYLOAD__", payloadPath.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("__PROC__", processName, StringComparison.Ordinal);

        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-validate-json-test",
            json,
            extension: "json");

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Recipe is valid", output);
        Assert.Contains("demo-recipe-json", output);
    }

    [Fact]
    public async Task Validate_when_payload_file_missing_returns_exit_code_1_and_lists_error()
    {
        // Arrange
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-validate-missing-payload",
            """
            {
              "name": "demo-recipe",
              "target": { "kind": "processByName", "processName": "explorer.exe" },
              "technique": { "name": "CreateRemoteThread" },
              "payload": { "path": "C:\\lab\\missing\\nofile.dll" },
              "safety": { "requireInteractiveConsent": true, "allowKernelDrivers": false }
            }
            """,
            extension: "json");

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("PAYLOAD_PATH_NOT_FOUND", output);
    }

    [Fact]
    public async Task Validate_with_json_flag_outputs_machine_readable_json()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string yaml = $"""
            name: demo-recipe
            target:
              kind: processByName
              processName: {Process.GetCurrentProcess().ProcessName}
            technique:
              name: CreateRemoteThread
            payload:
              path: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        string recipePath = CLITestHarness.CreateTempRecipeFile("runewire-validate-json-output", yaml);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("\"status\": \"valid\"", output);
        Assert.Contains("\"recipeName\": \"demo-recipe\"", output);
    }

    [Fact]
    public async Task Validate_preflight_failure_returns_exit_code_1_and_json_when_requested()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string yaml = $"""
            name: missing-target
            target:
              kind: processById
              processId: 999999
            technique:
              name: CreateRemoteThread
            payload:
              path: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        string recipePath = CLITestHarness.CreateTempRecipeFile("runewire-validate-preflight", yaml);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("TARGET_PID_NOT_FOUND", output);
    }

    [Fact]
    public async Task Validate_reports_driver_required_when_kernel_not_allowed()
    {
        // Arrange - use a technique that requires drivers (ProcessDoppelganging marked as such)
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string yaml = $"""
            name: driver-required
            target:
              kind: processByName
              processName: {Process.GetCurrentProcess().ProcessName}
            technique:
              name: ProcessDoppelganging
            payload:
              path: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        string recipePath = CLITestHarness.CreateTempRecipeFile("runewire-validate-driver-required", yaml);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("TECHNIQUE_DRIVER_REQUIRED", output);
    }

    [Fact]
    public async Task Validate_launch_target_with_missing_path_fails()
    {
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string yaml = $"""
            name: missing-launch-path
            target:
              kind: launchProcess
              path: ''
            technique:
              name: CreateRemoteThread
            payload:
              path: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        string recipePath = CLITestHarness.CreateTempRecipeFile("runewire-validate-launch-missing", yaml);

        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeValidateCommand.CommandName, recipePath);

        Assert.Equal(1, exitCode);
        Assert.Contains("TARGET_LAUNCH_PATH_REQUIRED", output);
    }
}
