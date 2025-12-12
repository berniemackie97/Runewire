using System.Text;

namespace Runewire.Cli.Tests;

/// <summary>
/// Shared helpers for invoking the Runewire CLI in tests.
/// Handles console redirection and temporary recipe file creation.
/// </summary>
public static class CLITestHarness
{
    /// <summary>
    /// Invokes the Runewire CLI entrypoint with the provided arguments and
    /// captures everything written to <see cref="Console.Out"/> and <see cref="Console.Error"/>.
    /// </summary>
    /// <param name="args">Arguments to pass to <see cref="Program.Main(string[])"/>.</param>
    /// <returns>
    /// A task that completes with a tuple:
    ///  - <c>exitCode</c>: the process exit code returned by the CLI.
    ///  - <c>stdout</c>: the captured console output.
    /// </returns>
    public static async Task<(int exitCode, string stdout)> RunWithCapturedOutputAsync(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalErr = Console.Error;

        StringBuilder sb = new();
        using StringWriter writer = new(sb);

        Console.SetOut(writer);
        Console.SetError(writer);

        try
        {
            int exitCode = await Program.Main(args);
            writer.Flush();
            return (exitCode, sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    /// <summary>
    /// Creates a temporary YAML recipe file on disk with the given contents.
    /// </summary>
    /// <param name="prefix">A prefix to include in the file name for debugging purposes.</param>
    /// <param name="contents">The YAML contents to write.</param>
    /// <returns>The full path to the created file.</returns>
    public static string CreateTempRecipeFile(string prefix, string contents)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, contents);
        return path;
    }
}
