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
        string nativePath = ResolveNativePath();
        bool nativePresent = !string.IsNullOrWhiteSpace(nativePath) && File.Exists(nativePath);

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
        if (nativePresent)
        {
            Assert.NotEqual(1, exitCode);
            Assert.Contains("Using native injection engine", output);
            Assert.Contains("demo-run", output);
        }
        else
        {
            Assert.Equal(3, exitCode);
            Assert.Contains("Native injector not found", output);
        }
    }

    [Fact]
    public async Task Run_with_native_and_injector_path_override_uses_override()
    {
        // Setup a fake path to force missing native case.
        string payloadPath = CLITestHarness.CreateTempPayloadFile();
        string recipePath = CLITestHarness.CreateTempRecipeFile(
            "runewire-engine-test",
            $"""
            name: demo-run
            description: Demo run via native engine.
            target:
              kind: processByName
              processName: {Process.GetCurrentProcess().ProcessName}
            technique:
              name: CreateRemoteThread
            payload:
              path: {payloadPath}
            """,
            extension: "yaml"
        );

        string fakePath = Path.Combine(Path.GetTempPath(), "does-not-exist", "Runewire.Injector.dll");

        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync("run", "--native", "--injector-path", fakePath, recipePath);

        Assert.Equal(3, exitCode);
        Assert.Contains(fakePath, output);
    }

    private static string ResolveNativePath()
    {
        string dllName = OperatingSystem.IsWindows() ? "Runewire.Injector.dll" : OperatingSystem.IsMacOS() ? "libRunewire.Injector.dylib" : "libRunewire.Injector.so";

        string? explicitPath = Environment.GetEnvironmentVariable("RUNEWIRE_INJECTOR_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        string? dir = Environment.GetEnvironmentVariable("RUNEWIRE_INJECTOR_DIR");
        if (!string.IsNullOrWhiteSpace(dir))
        {
            return Path.Combine(dir, dllName);
        }

        return Path.Combine(AppContext.BaseDirectory, dllName);
    }
}
