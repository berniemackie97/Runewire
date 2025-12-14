using System.CommandLine;
using Runewire.Cli.Commands;

namespace Runewire.Cli.Tests.Commands;

/// <summary>
/// Tests that the root command wiring does not drift.
/// If I break this, the CLI surface is changing and I want to know immediately.
/// </summary>
public sealed class RootCommandTests
{
    [Fact]
    public void BuildRootCommand_HasExpectedDescription()
    {
        // Arrange & Act
        RootCommand root = Program.BuildRootCommand();

        // Assert
        Assert.Equal("Runewire process injection lab CLI", root.Description);
    }

    [Fact]
    public void BuildRootCommand_ContainsRunAndValidateCommands()
    {
        // Arrange
        RootCommand root = Program.BuildRootCommand();

        // Act
        List<string> commandNames = [.. root.Children.OfType<Command>().Select(c => c.Name)];

        // Assert
        // Use the command constants so this test stays valid through renames.
        Assert.Contains(RecipeRunCommand.CommandName, commandNames);
        Assert.Contains(RecipeValidateCommand.CommandName, commandNames);
        Assert.Contains(TechniquesListCommand.CommandName, commandNames);
        Assert.Contains(RecipePreflightCommand.CommandName, commandNames);
    }

    [Fact]
    public async Task Main_WithHelpOption_ReturnsSuccessExitCode_AndPrintsHelp()
    {
        // Arrange
        string[] args = ["--help"];

        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(args);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Runewire process injection lab CLI", output);
        Assert.Contains(RecipeRunCommand.CommandName, output);
        Assert.Contains(RecipeValidateCommand.CommandName, output);
        Assert.Contains(TechniquesListCommand.CommandName, output);
        Assert.Contains(RecipePreflightCommand.CommandName, output);
    }
}
