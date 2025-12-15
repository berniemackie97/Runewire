using System.Runtime.InteropServices;

namespace Runewire.Cli.Tests;

public class IntegrationDemoRunTests
{
    [Fact]
    public async Task Run_self_suite_with_native_when_injector_available()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Native injector build is Windows-only today.
        }

        string repoRoot = GetRepoRoot();
        string injectorPath = Path.Combine(repoRoot, "native", "Runewire.Injector", "build", "Runewire.Injector.dll");
        if (!File.Exists(injectorPath))
        {
            return; // Skip when native injector is not present in CI.
        }

        string recipePath = Path.Combine(repoRoot, "demos", "recipes", "self-suite.yaml");
        string payloadPath = Path.Combine(repoRoot, "demos", "payloads", "shellcode.bin");

        Assert.True(File.Exists(recipePath), $"Demo recipe not found at {recipePath}");
        Assert.True(File.Exists(payloadPath), $"Demo payload not found at {payloadPath}");

        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(
            "run",
            "--native",
            "--injector-path", injectorPath,
            recipePath
        );

        Assert.Equal(0, exitCode);
        Assert.Contains("Injection succeeded", output);
    }

    private static string GetRepoRoot()
    {
        // tests/Runewire.Cli.Tests/bin/Debug/net8.0/
        string? current = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(current))
        {
            throw new InvalidOperationException("Unable to resolve base directory.");
        }

        return Path.GetFullPath(Path.Combine(current, "..", "..", "..", ".."));
    }
}
