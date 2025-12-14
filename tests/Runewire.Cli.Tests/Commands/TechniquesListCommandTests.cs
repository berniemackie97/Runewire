using Runewire.Cli.Commands;

namespace Runewire.Cli.Tests.Commands;

/// <summary>
/// Tests for the techniques listing command.
/// </summary>
public sealed class TechniquesListCommandTests
{
    [Fact]
    public async Task Techniques_command_lists_built_in_techniques()
    {
        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(TechniquesListCommand.CommandName);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Supported injection techniques", output);

        // At least the seeded built-in technique should be listed.
        Assert.Contains("CreateRemoteThread", output);
    }

    [Fact]
    public async Task Techniques_command_supports_json_output()
    {
        // Act
        (int exitCode, string output) = await CLITestHarness.RunWithCapturedOutputAsync(TechniquesListCommand.CommandName, "--json");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("\"status\": \"ok\"", output);
        Assert.Contains("\"techniques\"", output);
        Assert.Contains("\"name\": \"CreateRemoteThread\"", output);
        Assert.Contains("\"platforms\"", output);
    }
}
