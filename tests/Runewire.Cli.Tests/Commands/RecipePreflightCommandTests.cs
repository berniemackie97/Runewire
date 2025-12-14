using System.Diagnostics;
using Runewire.Cli.Commands;

namespace Runewire.Cli.Tests.Commands;

/// <summary>
/// Tests for the preflight command.
/// </summary>
public sealed class RecipePreflightCommandTests
{
    [Fact]
    public async Task Preflight_valid_recipe_returns_exit_code_0()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string processName = Process.GetCurrentProcess().ProcessName;

        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-preflight-test",
            $"""
            name: demo-preflight
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
            """);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipePreflightCommand.CommandName, recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("passed preflight", output);
    }

    [Fact]
    public async Task Preflight_json_output_returns_machine_readable_summary()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string processName = Process.GetCurrentProcess().ProcessName;

        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-preflight-json-test",
            $"""
            name: demo-preflight-json
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
            """);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipePreflightCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("\"status\": \"valid\"", output);
        Assert.Contains("\"preflight\"", output);
    }

    [Fact]
    public async Task Preflight_missing_target_returns_exit_code_1_and_error()
    {
        // Arrange
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-preflight-fail",
            $"""
            name: demo-preflight-fail
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
            """);

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(RecipePreflightCommand.CommandName, "--json", recipePath);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("TARGET_PID_NOT_FOUND", output);
    }
}
