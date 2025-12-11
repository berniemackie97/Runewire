using System.Text;

namespace Runewire.Cli.Tests;

public class EngineSelectionTests
{
    [Fact]
    public void Run_with_native_flag_prints_native_hint_and_returns_non_error()
    {
        // Setup
        string recipePath = CreateTempFile(
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
        (int exitCode, string output) = RunWithCapturedOutput("run", "--native", recipePath);

        // Assert
        // For now we only assert that the CLI recognizes the flag and
        // emits a clear message about using the native engine. The actual
        // wiring to NativeInjectionEngine will come in the next step.
        Assert.NotEqual(1, exitCode); // should not be a hard failure
        Assert.Contains("Using native injection engine", output);
        Assert.Contains("demo-run", output);
    }

    private static (int exitCode, string stdout) RunWithCapturedOutput(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalErr = Console.Error;

        StringBuilder sb = new();
        using StringWriter writer = new(sb);

        Console.SetOut(writer);
        Console.SetError(writer);

        try
        {
            int exitCode = Program.Main(args);
            writer.Flush();
            return (exitCode, sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static string CreateTempFile(string contents)
    {
        string path = Path.Combine(Path.GetTempPath(), $"runewire-engine-test-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, contents);
        return path;
    }
}
