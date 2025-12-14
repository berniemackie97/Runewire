namespace Runewire.Cli.Infrastructure;

/// <summary>
/// Tiny console helper for the CLI.
/// Keeps output consistent and readable (colors, headers, bullets).
/// I centralized this so every command is not doing its own Console.WriteLine chaos.
/// </summary>
internal static class CliConsole
{
    public static void WriteSuccess(string message) => WriteLineWithColor(message, ConsoleColor.Green);

    public static void WriteError(string message) => WriteLineWithColor(message, ConsoleColor.Red);

    public static void WriteDetail(string message) => WriteLineWithColor(message, ConsoleColor.DarkGray);

    public static void WriteHeader(string message, ConsoleColor color) => WriteLineWithColor(message, color);

    public static void WriteBullet(string message, ConsoleColor color) => WriteLineWithColor($" - {message}", color);

    private static void WriteLineWithColor(string message, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }
        finally
        {
            // Always restore so we do not accidentally tint the whole terminal.
            Console.ForegroundColor = original;
        }
    }
}
