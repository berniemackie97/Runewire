using System.Diagnostics;

namespace Runewire.Cli.Tests;

/// <summary>
/// Engine selection tests.
/// </summary>
public sealed class EngineSelectionTests
{
    [Fact]
    public async Task Run_with_native_flag_prints_native_hint_and_returns_non_error()
    {
        // Setup
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string processName = Process.GetCurrentProcess().ProcessName;
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-engine-test",
            $"""
            name: demo-run
            description: Demo run via native engine.
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

        // Run
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("run", "--native", recipePath);

        // Assert
        Assert.NotEqual(1, exitCode);
        Assert.Contains("Using native injection engine", output);
        Assert.Contains("demo-run", output);
    }
}
