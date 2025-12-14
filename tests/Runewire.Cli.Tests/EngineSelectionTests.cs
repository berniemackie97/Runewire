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
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-engine-test",
            """
            name: demo-run
            description: Demo run via native engine.
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
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("run", "--native", recipePath);

        // Assert
        Assert.NotEqual(1, exitCode);
        Assert.Contains("Using native injection engine", output);
        Assert.Contains("demo-run", output);
    }
}
