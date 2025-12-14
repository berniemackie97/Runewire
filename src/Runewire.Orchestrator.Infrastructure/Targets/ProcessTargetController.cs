using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Targets;

/// <summary>
/// Windows process controller that can suspend and resume a target process.
/// </summary>
public sealed class ProcessTargetController : ITargetController
{
    public Task<TargetControlResult> SuspendAsync(RecipeTarget target, CancellationToken cancellationToken = default)
    {
        return ControlAsync(target, suspend: true, cancellationToken);
    }

    public Task<TargetControlResult> ResumeAsync(RecipeTarget target, CancellationToken cancellationToken = default)
    {
        return ControlAsync(target, suspend: false, cancellationToken);
    }

    private static Task<TargetControlResult> ControlAsync(RecipeTarget target, bool suspend, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_UNSUPPORTED_PLATFORM", "Target suspend/resume is only supported on Windows right now."));
        }

        if (target.Kind == RecipeTargetKind.LaunchProcess)
        {
            return Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_UNSUPPORTED_TARGET", "Suspend/resume for launch targets is not supported yet."));
        }

        if (target.Kind == RecipeTargetKind.Self)
        {
            return Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_UNSUPPORTED_TARGET", "Suspend/resume is not allowed when targeting self."));
        }

        int? pid = ResolveProcessId(target);
        if (pid is null || pid <= 0)
        {
            return Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_PROCESS_NOT_FOUND", "Target process could not be resolved."));
        }

        try
        {
            using SafeProcessHandle handle = OpenProcess(ProcessAccess.SuspendResume, false, (uint)pid);
            if (handle.IsInvalid)
            {
                return Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_OPEN_FAILED", $"Failed to open process {pid}."));
            }

            int status = suspend ? NtSuspendProcess(handle) : NtResumeProcess(handle);
            if (status != 0)
            {
                return Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_FAILED", $"Failed to {(suspend ? "suspend" : "resume")} process {pid} (status {status})."));
            }

            return Task.FromResult(TargetControlResult.Succeeded());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TargetControlResult.Failed("TARGET_CONTROL_EXCEPTION", ex.Message));
        }
    }

    private static int? ResolveProcessId(RecipeTarget target)
    {
        return target.Kind switch
        {
            RecipeTargetKind.Self => Environment.ProcessId,
            RecipeTargetKind.ProcessById => target.ProcessId,
            RecipeTargetKind.ProcessByName => ResolveByName(target.ProcessName),
            RecipeTargetKind.LaunchProcess => null, // we do not control launched processes from here yet
            _ => null
        };
    }

    private static int? ResolveByName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        try
        {
            Process? proc = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)).FirstOrDefault();
            return proc?.Id;
        }
        catch
        {
            return null;
        }
    }

    [Flags]
    private enum ProcessAccess : uint
    {
        SuspendResume = 0x0800,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(ProcessAccess desiredAccess, bool inheritHandle, uint processId);

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(SafeHandle processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(SafeHandle processHandle);
}
