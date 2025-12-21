using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Targets;

/// <summary>
/// Launches processes for launch targets.
/// </summary>
public sealed class ProcessTargetLauncher : ITargetLauncher
{
    public Task<TargetLaunchResult> LaunchAsync(RecipeTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        if (target.Kind != RecipeTargetKind.LaunchProcess)
        {
            return Task.FromResult(TargetLaunchResult.Failed("TARGET_LAUNCH_INVALID", "Target kind must be LaunchProcess."));
        }

        if (string.IsNullOrWhiteSpace(target.LaunchPath))
        {
            return Task.FromResult(TargetLaunchResult.Failed("TARGET_LAUNCH_PATH_REQUIRED", "Launch path is required."));
        }

        if (!File.Exists(target.LaunchPath))
        {
            return Task.FromResult(TargetLaunchResult.Failed("TARGET_LAUNCH_PATH_NOT_FOUND", $"Launch path not found: {target.LaunchPath}"));
        }

        if (!string.IsNullOrWhiteSpace(target.LaunchWorkingDirectory) && !Directory.Exists(target.LaunchWorkingDirectory))
        {
            return Task.FromResult(TargetLaunchResult.Failed("TARGET_LAUNCH_WORKDIR_NOT_FOUND", $"Launch working directory not found: {target.LaunchWorkingDirectory}"));
        }

        if (OperatingSystem.IsWindows())
        {
            return Task.FromResult(LaunchWindows(target));
        }

        if (target.LaunchStartSuspended)
        {
            return Task.FromResult(TargetLaunchResult.Failed("TARGET_LAUNCH_SUSPEND_UNSUPPORTED", "Start suspended is only supported on Windows."));
        }

        return Task.FromResult(LaunchDefault(target));
    }

    [SupportedOSPlatform("windows")]
    private static TargetLaunchResult LaunchWindows(RecipeTarget target)
    {
        if (!target.LaunchStartSuspended)
        {
            return LaunchDefault(target);
        }

        try
        {
            STARTUPINFO startupInfo = new()
            {
                cb = Marshal.SizeOf<STARTUPINFO>()
            };

            PROCESS_INFORMATION processInfo;
            string commandLineText = BuildCommandLine(target.LaunchPath!, target.LaunchArguments);
            StringBuilder commandLine = new(commandLineText);

            bool success = CreateProcess(
                target.LaunchPath,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CreateProcessFlags.CREATE_SUSPENDED,
                IntPtr.Zero,
                target.LaunchWorkingDirectory,
                ref startupInfo,
                out processInfo);

            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                return TargetLaunchResult.Failed("TARGET_LAUNCH_FAILED", $"CreateProcess failed with error {error}.");
            }

            try
            {
                return TargetLaunchResult.Succeeded((int)processInfo.dwProcessId);
            }
            finally
            {
                if (processInfo.hThread != IntPtr.Zero)
                {
                    _ = CloseHandle(processInfo.hThread);
                }

                if (processInfo.hProcess != IntPtr.Zero)
                {
                    _ = CloseHandle(processInfo.hProcess);
                }
            }
        }
        catch (Exception ex)
        {
            return TargetLaunchResult.Failed("TARGET_LAUNCH_FAILED", ex.Message);
        }
    }

    private static TargetLaunchResult LaunchDefault(RecipeTarget target)
    {
        try
        {
            ProcessStartInfo startInfo = new(target.LaunchPath!)
            {
                UseShellExecute = false
            };

            if (!string.IsNullOrWhiteSpace(target.LaunchArguments))
            {
                startInfo.Arguments = target.LaunchArguments;
            }

            if (!string.IsNullOrWhiteSpace(target.LaunchWorkingDirectory))
            {
                startInfo.WorkingDirectory = target.LaunchWorkingDirectory;
            }

            Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return TargetLaunchResult.Failed("TARGET_LAUNCH_FAILED", "Process could not be started.");
            }

            int pid = process.Id;
            process.Dispose();
            return TargetLaunchResult.Succeeded(pid);
        }
        catch (Exception ex)
        {
            return TargetLaunchResult.Failed("TARGET_LAUNCH_FAILED", ex.Message);
        }
    }

    private static string BuildCommandLine(string launchPath, string? arguments)
    {
        string escapedPath = $"\"{launchPath}\"";
        return string.IsNullOrWhiteSpace(arguments) ? escapedPath : $"{escapedPath} {arguments}";
    }

    [Flags]
    private enum CreateProcessFlags : uint
    {
        CREATE_SUSPENDED = 0x00000004,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        CreateProcessFlags dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
