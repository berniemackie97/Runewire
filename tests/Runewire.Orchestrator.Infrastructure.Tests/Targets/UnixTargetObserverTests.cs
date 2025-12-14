using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Infrastructure.Targets;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Tests.Targets;

public sealed class UnixTargetObserverTests
{
    [Fact]
    public async Task WaitForAsync_file_exists_succeeds()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string tempPath = Path.Combine(Path.GetTempPath(), $"runewire-file-{Guid.NewGuid():N}");
        File.WriteAllText(tempPath, "data");

        UnixTargetObserver observer = new();
        WaitCondition condition = new(WaitConditionKind.FileExists, tempPath, 1000);

        try
        {
            WaitResult result = await observer.WaitForAsync(RecipeTarget.Self(), condition);
            Assert.True(result.Success);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task WaitForAsync_process_exit_succeeds_when_process_not_found()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixTargetObserver observer = new();
        RecipeTarget target = RecipeTarget.ForProcessId(int.MaxValue); // assume nonexistent
        WaitCondition condition = new(WaitConditionKind.ProcessExited, string.Empty, 100);

        WaitResult result = await observer.WaitForAsync(target, condition);

        Assert.True(result.Success); // treated as already exited
    }
}
