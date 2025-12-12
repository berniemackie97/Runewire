namespace Runewire.Cli.Tests;

/// <summary>
/// Tests around engine-selection behavior (e.g., the <c>--native</c> flag).
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
        // For now we only assert that the CLI recognizes the flag and
        // emits a clear message about using the native engine. The actual
        // wiring to NativeInjectionEngine will come in a later step.
        Assert.NotEqual(1, exitCode); // should not be a hard failure
        Assert.Contains("Using native injection engine", output);
        Assert.Contains("demo-run", output);
    }
}
