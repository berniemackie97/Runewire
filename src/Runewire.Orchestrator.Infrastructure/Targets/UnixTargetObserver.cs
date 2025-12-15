using System.Diagnostics;
using System.IO;
using System.Text;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Targets;

/// <summary>
/// Cross-platform observer for wait conditions on Unix-like systems.
/// Supports process exit, file existence, and named pipe (Unix domain socket/FIFO) checks.
/// Other conditions return a clear unsupported error.
/// </summary>
public sealed class UnixTargetObserver : ITargetObserver
{
    public Task<WaitResult> WaitForAsync(RecipeTarget target, WaitCondition condition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        cancellationToken.ThrowIfCancellationRequested();

        return condition.Kind switch
        {
            WaitConditionKind.FileExists => WaitForFileAsync(condition, cancellationToken),
            WaitConditionKind.ProcessExited => WaitForProcessExitAsync(target, condition, cancellationToken),
            WaitConditionKind.NamedPipeAvailable => WaitForPipeAsync(condition, cancellationToken),
            WaitConditionKind.SharedMemoryValueEquals => WaitForSharedMemoryValueAsync(condition, cancellationToken),
            _ => Task.FromResult(WaitResult.Failed("WAIT_CONDITION_UNSUPPORTED_PLATFORM", $"Wait condition '{condition.Kind}' is not supported on this platform.")),
        };
    }

    private static async Task<WaitResult> WaitForFileAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "File path is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(condition.Value))
            {
                return WaitResult.Succeeded();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for file '{condition.Value}'.");
    }

    private static async Task<WaitResult> WaitForPipeAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Pipe path is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(condition.Value))
            {
                return WaitResult.Succeeded();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for pipe '{condition.Value}'.");
    }

    private static async Task<WaitResult> WaitForSharedMemoryValueAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Shared memory condition is required (Name=Value).");
        }

        int equalsIndex = condition.Value.IndexOf('=');
        if (equalsIndex <= 0 || equalsIndex >= condition.Value.Length - 1)
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Shared memory condition format is invalid.");
        }

        string name = condition.Value[..equalsIndex];
        string expected = condition.Value[(equalsIndex + 1)..];

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string path = name.StartsWith("/", StringComparison.Ordinal) ? name : $"/dev/shm/{name}";
                if (File.Exists(path))
                {
                    string content = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    if (content.Contains(expected, StringComparison.Ordinal))
                    {
                        return WaitResult.Succeeded();
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // not yet present
            }
            catch
            {
                // ignore other errors
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for shared memory '{name}' to contain expected value.");
    }

    private static async Task<WaitResult> WaitForProcessExitAsync(RecipeTarget target, WaitCondition condition, CancellationToken cancellationToken)
    {
        int? pid = target.Kind switch
        {
            RecipeTargetKind.ProcessById => target.ProcessId,
            RecipeTargetKind.Self => Environment.ProcessId,
            _ => null
        };

        if (pid is null or <= 0)
        {
            return WaitResult.Failed("WAIT_CONDITION_PROCESS_NOT_FOUND", "Target process could not be resolved.");
        }

        TimeSpan timeout = GetTimeout(condition);

        try
        {
            using Process process = Process.GetProcessById(pid.Value);
            bool exited = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds), cancellationToken).ConfigureAwait(false);
            return exited ? WaitResult.Succeeded() : WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for process {pid.Value} to exit.");
        }
        catch (ArgumentException)
        {
            return WaitResult.Succeeded(); // already exited
        }
        catch (Exception ex)
        {
            return WaitResult.Failed("WAIT_CONDITION_FAILED", ex.Message);
        }
    }

    private static TimeSpan GetTimeout(WaitCondition condition)
    {
        int milliseconds = condition.TimeoutMilliseconds ?? 30_000;
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
