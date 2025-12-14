using System.Text;

namespace Runewire.Cli.Tests;

/// <summary>
/// Test helper for running the CLI and capturing output.
/// </summary>
internal static class CLITestHarness
{
    /// <summary>
    /// Run the CLI entry point and capture stdout + stderr.
    /// </summary>
    public static async Task<(int exitCode, string stdout)> RunWithCapturedOutputAsync(params string[] args)
    {
        args ??= [];

        TextWriter originalOut = Console.Out;
        TextWriter originalErr = Console.Error;

        StringBuilder sb = new();
        using StringWriter writer = new(sb);

        Console.SetOut(writer);
        Console.SetError(writer);

        try
        {
            int exitCode = await Program.Main(args).ConfigureAwait(false);

            // StringWriter writes into the StringBuilder already. Flush is just to be safe.
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
    /// Create a temp YAML recipe file and return the path.
    /// Prefix is just to make debugging temp files less annoying.
    /// </summary>
    public static string CreateTempRecipeFile(string prefix, string contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(contents);

        string path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.yaml");

        File.WriteAllText(path, contents);
        return path;
    }
}
