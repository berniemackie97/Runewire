using Runewire.Cli.Commands;
using System.Diagnostics;

namespace Runewire.Cli.Tests.Commands;

/// <summary>
/// End to end tests for run.
/// </summary>
public sealed class RecipeRunCommandTests
{
    [Fact]
    public async Task Run_valid_recipe_returns_exit_code_0_and_reports_success()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string processName = Process.GetCurrentProcess().ProcessName;
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-run-test",
            $"""
            name: demo-run
            description: Demo run execution.
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
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Injection succeeded", output);
        Assert.Contains("demo-run", output);
    }

    [Fact]
    public async Task Run_invalid_recipe_returns_exit_code_1_and_lists_errors()
    {
        // Arrange
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

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, recipePath);

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
        // Arrange
        string recipePath = Path.Combine(Path.GetTempPath(), $"runewire-run-missing-{Guid.NewGuid():N}.yaml");

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("Recipe file not found", output);
    }

    [Fact]
    public async Task Run_missing_file_with_json_returns_structured_error()
    {
        // Arrange
        string recipePath = Path.Combine(Path.GetTempPath(), $"runewire-run-missing-{Guid.NewGuid():N}.yaml");

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("\"status\": \"error\"", output);
        Assert.Contains("\"engine\": \"dry-run\"", output);
    }

    [Fact]
    public async Task Run_json_recipe_returns_exit_code_0_and_reports_success()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string processName = Process.GetCurrentProcess().ProcessName;
        string jsonTemplate = """
            {
              "name": "demo-run-json",
              "description": "Demo run via JSON.",
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
            "runewire-run-json-test",
            json,
            extension: "json");

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Injection succeeded", output);
        Assert.Contains("demo-run-json", output);
    }

    [Fact]
    public async Task Run_when_payload_file_missing_returns_exit_code_1_and_lists_error()
    {
        // Arrange
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-run-missing-payload",
            """
            name: demo-run
            target:
              kind: processByName
              processName: explorer.exe
            technique:
              name: CreateRemoteThread
            payload:
              path: C:\lab\missing\nofile.dll
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("PAYLOAD_PATH_NOT_FOUND", output);
    }

    [Fact]
    public async Task Run_with_json_flag_outputs_machine_readable_json()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string yaml = $"""
            name: json-output
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

        string recipePath = CLITestHarness.CreateTempRecipeFile("runewire-run-json-output", yaml);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("\"status\": \"succeeded\"", output);
        Assert.Contains("\"recipeName\": \"json-output\"", output);
        Assert.DoesNotContain("Dry-run injection plan", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_preflight_failure_returns_exit_code_1_and_json_when_requested()
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

        string recipePath = CLITestHarness.CreateTempRecipeFile("runewire-run-preflight", yaml);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("TARGET_PID_NOT_FOUND", output);
    }

    [Fact]
    public async Task Run_with_steps_in_json_mode_includes_step_results()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string processName = Process.GetCurrentProcess().ProcessName;
        string yaml = $"""
            name: workflow-json
            target:
              kind: processByName
              processName: {processName}
            technique:
              name: CreateRemoteThread
            payload:
              path: {payloadPath}
            steps:
              - kind: injectTechnique
                techniqueName: CreateRemoteThread
                payloadPath: {payloadPath}
              - kind: wait
                condition:
                  kind: file
                  value: {payloadPath}
              - kind: injectTechnique
                techniqueName: CreateRemoteThread
                payloadPath: {payloadPath}
            safety:
              requireInteractiveConsent: true
              allowKernelDrivers: false
            """;

        string recipePath = CLITestHarness.CreateTempRecipeFile("runewire-run-steps-json", yaml);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipeRunCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("\"steps\"", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InjectTechnique", output);
    }
}
