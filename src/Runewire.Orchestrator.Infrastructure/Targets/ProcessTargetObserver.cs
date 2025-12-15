using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Net.Sockets;
using System.IO.MemoryMappedFiles;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Net;
using System.Net.Http;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Targets;

[SupportedOSPlatform("windows")]
/// <summary>
/// Observes process state (Windows) to satisfy wait conditions like module load.
/// </summary>
public sealed class ProcessTargetObserver : ITargetObserver
{
    public Task<WaitResult> WaitForAsync(RecipeTarget target, WaitCondition condition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(WaitResult.Failed("WAIT_CONDITION_UNSUPPORTED_PLATFORM", "Wait conditions are only supported on Windows right now."));
        }

        if (target.Kind == RecipeTargetKind.LaunchProcess)
        {
            return Task.FromResult(WaitResult.Failed("WAIT_CONDITION_UNSUPPORTED_TARGET", "Wait conditions for launch targets are not supported yet."));
        }

        int? pid = ResolveProcessId(target);
        if (pid is null || pid <= 0)
        {
            return Task.FromResult(WaitResult.Failed("WAIT_CONDITION_PROCESS_NOT_FOUND", "Target process could not be resolved."));
        }

        return condition.Kind switch
        {
            WaitConditionKind.ModuleLoaded => WaitForModuleAsync(pid.Value, condition, cancellationToken),
            WaitConditionKind.FileExists => WaitForFileAsync(condition, cancellationToken),
            WaitConditionKind.ProcessExited => WaitForProcessExitAsync(pid.Value, condition, cancellationToken),
            WaitConditionKind.WindowClass => WaitForWindowAsync(pid.Value, condition, matchTitle: false, cancellationToken),
            WaitConditionKind.WindowTitle => WaitForWindowAsync(pid.Value, condition, matchTitle: true, cancellationToken),
            WaitConditionKind.NamedPipeAvailable => WaitForNamedPipeAsync(condition, cancellationToken),
            WaitConditionKind.ProcessHandleReady => WaitForHandleAsync(pid.Value, condition, cancellationToken),
            WaitConditionKind.TcpPortListening => WaitForTcpPortAsync(condition, cancellationToken),
            WaitConditionKind.NamedEvent => WaitForNamedSyncAsync(condition, SyncKind.Event, cancellationToken),
            WaitConditionKind.NamedMutex => WaitForNamedSyncAsync(condition, SyncKind.Mutex, cancellationToken),
            WaitConditionKind.NamedSemaphore => WaitForNamedSyncAsync(condition, SyncKind.Semaphore, cancellationToken),
            WaitConditionKind.SharedMemoryExists => WaitForSharedMemoryAsync(condition, cancellationToken),
            WaitConditionKind.RegistryValueEquals => WaitForRegistryValueAsync(condition, cancellationToken),
            WaitConditionKind.ChildProcessAppeared => WaitForChildProcessAsync(pid.Value, condition, cancellationToken),
            WaitConditionKind.HttpReachable => WaitForHttpAsync(condition, cancellationToken),
            WaitConditionKind.ServiceState => WaitForServiceStateAsync(condition, cancellationToken),
            WaitConditionKind.EnvironmentVariableEquals => WaitForEnvironmentAsync(condition, cancellationToken),
            WaitConditionKind.FileContentContains => WaitForFileContentAsync(condition, cancellationToken),
            WaitConditionKind.SharedMemoryValueEquals => WaitForSharedMemoryValueAsync(condition, cancellationToken),
            _ => Task.FromResult(WaitResult.Failed("WAIT_CONDITION_UNKNOWN", $"Wait condition '{condition.Kind}' is not supported.")),
        };
    }

    private static async Task<WaitResult> WaitForModuleAsync(int pid, WaitCondition condition, CancellationToken cancellationToken)
    {
        string moduleName = condition.Value;
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Module name is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using Process process = Process.GetProcessById(pid);
                foreach (ProcessModule module in process.Modules)
                {
                    if (string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase) ||
                        module.FileName.EndsWith(moduleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return WaitResult.Succeeded();
                    }
                }
            }
            catch (ArgumentException)
            {
                return WaitResult.Failed("WAIT_CONDITION_PROCESS_EXITED", $"Process {pid} exited before module '{moduleName}' loaded.");
            }
            catch (Win32Exception)
            {
                // Ignore transient access issues and keep polling.
            }
            catch (InvalidOperationException)
            {
                // Process modules can throw while enumerating; continue polling.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for module '{moduleName}' in process {pid}.");
    }

    private static async Task<WaitResult> WaitForFileAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        string path = condition.Value;
        if (string.IsNullOrWhiteSpace(path))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "File path is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                return WaitResult.Succeeded();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for file '{path}'.");
    }

    private static async Task<WaitResult> WaitForProcessExitAsync(int pid, WaitCondition condition, CancellationToken cancellationToken)
    {
        TimeSpan timeout = GetTimeout(condition);
        try
        {
            using Process process = Process.GetProcessById(pid);
            Task<bool> waitTask = Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds), cancellationToken);
            bool exited = await waitTask.ConfigureAwait(false);
            return exited ? WaitResult.Succeeded() : WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for process {pid} to exit.");
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

    private static async Task<WaitResult> WaitForWindowAsync(int pid, WaitCondition condition, bool matchTitle, CancellationToken cancellationToken)
    {
        string value = condition.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Window class/title is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool found = EnumWindows((hWnd, lParam) =>
            {
                _ = lParam;
                GetWindowThreadProcessId(hWnd, out uint windowPid);
                if (windowPid != pid)
                {
                    return true; // continue
                }

                if (matchTitle)
                {
                    string title = GetWindowText(hWnd);
                    if (title.Contains(value, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // stop enumeration
                    }
                }
                else
                {
                    string cls = GetWindowClass(hWnd);
                    if (cls.Equals(value, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // stop enumeration
                    }
                }

                return true;
            }, IntPtr.Zero);

            if (!found)
            {
                return WaitResult.Succeeded();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        string target = matchTitle ? "window title" : "window class";
        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for {target} '{value}' in process {pid}.");
    }

    private static async Task<WaitResult> WaitForNamedPipeAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        string pipeName = condition.Value;
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Pipe name is required.");
        }

        string fullName = pipeName.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)
            ? pipeName
            : $@"\\.\pipe\{pipeName}";

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (WaitNamedPipe(fullName, 0))
            {
                return WaitResult.Succeeded();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for named pipe '{pipeName}'.");
    }

    private static async Task<WaitResult> WaitForHandleAsync(int pid, WaitCondition condition, CancellationToken cancellationToken)
    {
        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using SafeProcessHandle handle = OpenProcess(ProcessAccess.QueryLimitedInformation, false, (uint)pid);
            if (!handle.IsInvalid)
            {
                return WaitResult.Succeeded();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for process handle {pid} to be available.");
    }

    private static async Task<WaitResult> WaitForTcpPortAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Host:port is required.");
        }

        string host;
        int port;
        if (!TryParseHostPort(condition.Value, out host, out port))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Host:port format is invalid.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using TcpClient client = new();
                var connectTask = client.ConnectAsync(host, port);
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(500));
                await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);
                if (client.Connected)
                {
                    return WaitResult.Succeeded();
                }
            }
            catch
            {
                // swallow and retry
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for TCP listener at {host}:{port}.");
    }

    private enum SyncKind
    {
        Event,
        Mutex,
        Semaphore
    }

    private static async Task<WaitResult> WaitForNamedSyncAsync(WaitCondition condition, SyncKind kind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Name is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                switch (kind)
                {
                    case SyncKind.Event:
                        using (EventWaitHandle.OpenExisting(condition.Value))
                        {
                            return WaitResult.Succeeded();
                        }
                    case SyncKind.Mutex:
                        using (Mutex.OpenExisting(condition.Value))
                        {
                            return WaitResult.Succeeded();
                        }
                    case SyncKind.Semaphore:
                        using (Semaphore.OpenExisting(condition.Value))
                        {
                            return WaitResult.Succeeded();
                        }
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // not present yet
            }
            catch
            {
                // ignore other transient errors
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for {kind} '{condition.Value}'.");
    }

    private static async Task<WaitResult> WaitForSharedMemoryAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Shared memory name is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (MemoryMappedFile.OpenExisting(condition.Value))
                {
                    return WaitResult.Succeeded();
                }
            }
            catch (FileNotFoundException)
            {
                // not yet present
            }
            catch
            {
                // other errors ignored
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for shared memory '{condition.Value}'.");
    }

    private static async Task<WaitResult> WaitForRegistryValueAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Registry path is required.");
        }

        if (!TryParseRegistryCondition(condition.Value, out RegistryHive hive, out string? path, out string? valueName, out string? expected))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Registry condition format is invalid.");
        }

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(valueName))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Registry condition format is invalid.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using RegistryKey? baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using RegistryKey? key = baseKey.OpenSubKey(path);
                if (key is not null)
                {
                    object? val = key.GetValue(valueName);
                    if (val is not null && string.Equals(Convert.ToString(val), expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return WaitResult.Succeeded();
                    }
                }
            }
            catch
            {
                // ignore transient errors
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for registry value '{condition.Value}'.");
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
                using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(name))
                using (MemoryMappedViewStream stream = mmf.CreateViewStream())
                using (StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 512, leaveOpen: false))
                {
                    string content = await reader.ReadToEndAsync().ConfigureAwait(false);
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

    private static async Task<WaitResult> WaitForChildProcessAsync(int parentPid, WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Child process name is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FindChildProcess(parentPid, condition.Value))
            {
                return WaitResult.Succeeded();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for child process '{condition.Value}'.");
    }

    private static bool FindChildProcess(int parentPid, string processName)
    {
        PROCESSENTRY32 entry = new();
        entry.dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>();

        using SafeSnapshotHandle snapshot = CreateToolhelp32Snapshot(ToolhelpSnapshotFlags.Process, 0);
        if (snapshot.IsInvalid)
        {
            return false;
        }

        if (!Process32First(snapshot, ref entry))
        {
            return false;
        }

        do
        {
            if (entry.th32ParentProcessID == parentPid)
            {
                if (string.Equals(entry.szExeFile, processName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        } while (Process32Next(snapshot, ref entry));

        return false;
    }

    private static async Task<WaitResult> WaitForHttpAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "URL is required.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        using HttpClient client = new();

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using HttpRequestMessage req = new(HttpMethod.Head, condition.Value);
                using HttpResponseMessage resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    return WaitResult.Succeeded();
                }
            }
            catch
            {
                // ignore and retry
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for HTTP reachability '{condition.Value}'.");
    }

    private static async Task<WaitResult> WaitForServiceStateAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return WaitResult.Failed("WAIT_CONDITION_UNSUPPORTED_PLATFORM", "Service state wait is only supported on Windows.");
        }

        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Service condition is required (ServiceName=State).");
        }

        if (!TryParseServiceCondition(condition.Value, out string? serviceName, out string? desiredState))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Service condition format is invalid.");
        }

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        string serviceNameNonNull = serviceName!;
        string desiredStateNonNull = desiredState!;
        ServiceControllerStatus targetStatus = ParseServiceState(desiredStateNonNull);

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using ServiceController controller = new(serviceNameNonNull);
                controller.Refresh();
                if (controller.Status == targetStatus)
                {
                    return WaitResult.Succeeded();
                }
            }
            catch
            {
                // ignore and retry
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for service '{serviceNameNonNull}' to reach state '{desiredStateNonNull}'.");
    }

    private static async Task<WaitResult> WaitForEnvironmentAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Environment condition is required (NAME=VALUE).");
        }

        int equalsIndex = condition.Value.IndexOf('=');
        if (equalsIndex <= 0 || equalsIndex >= condition.Value.Length - 1)
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "Environment condition format is invalid.");
        }

        string name = condition.Value[..equalsIndex];
        string expected = condition.Value[(equalsIndex + 1)..];

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? current = Environment.GetEnvironmentVariable(name);
            if (current is not null && string.Equals(current, expected, StringComparison.Ordinal))
            {
                return WaitResult.Succeeded();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for environment variable '{name}' to equal '{expected}'.");
    }

    private static async Task<WaitResult> WaitForFileContentAsync(WaitCondition condition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "File condition is required (path|contains).");
        }

        string[] parts = condition.Value.Split('|', 2, StringSplitOptions.None);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return WaitResult.Failed("WAIT_CONDITION_VALUE_REQUIRED", "File condition format is invalid.");
        }

        string path = parts[0];
        string needle = parts[1];

        TimeSpan timeout = GetTimeout(condition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(path))
                {
                    string content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                    if (content.Contains(needle, StringComparison.Ordinal))
                    {
                        return WaitResult.Succeeded();
                    }
                }
            }
            catch
            {
                // ignore transient IO errors
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
        }

        return WaitResult.Failed("WAIT_CONDITION_TIMEOUT", $"Timed out waiting for file '{path}' to contain expected text.");
    }

    // Service state support is stubbed out in this build; method above returns unsupported.

    private static TimeSpan GetTimeout(WaitCondition condition)
    {
        int milliseconds = condition.TimeoutMilliseconds ?? 30_000;
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static int? ResolveProcessId(RecipeTarget target)
    {
        return target.Kind switch
        {
            RecipeTargetKind.Self => Environment.ProcessId,
            RecipeTargetKind.ProcessById => target.ProcessId,
            RecipeTargetKind.ProcessByName => ResolveByName(target.ProcessName),
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

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WaitNamedPipe(string lpNamedPipeName, uint nTimeOut);

    private static string GetWindowText(IntPtr hWnd)
    {
        System.Text.StringBuilder sb = new(512);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetWindowClass(IntPtr hWnd)
    {
        System.Text.StringBuilder sb = new(256);
        _ = GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static bool TryParseHostPort(string value, out string host, out int port)
    {
        host = "127.0.0.1";
        port = 0;

        string[] parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return int.TryParse(parts[0], out port);
        }

        if (parts.Length == 2 && int.TryParse(parts[1], out port))
        {
            host = parts[0];
            return true;
        }

        return false;
    }

    private static bool TryParseRegistryCondition(string value, out RegistryHive hive, out string? path, out string? valueName, out string? expected)
    {
        hive = RegistryHive.CurrentUser;
        path = null;
        valueName = null;
        expected = null;

        // Format: HIVE\Key\SubKey:ValueName=Expected
        int equalsIndex = value.IndexOf('=');
        if (equalsIndex <= 0 || equalsIndex >= value.Length - 1)
        {
            return false;
        }

        expected = value[(equalsIndex + 1)..];

        string left = value[..equalsIndex];
        int colonIndex = left.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex >= left.Length - 1)
        {
            return false;
        }

        valueName = left[(colonIndex + 1)..];
        string hiveAndPath = left[..colonIndex];
        int firstSlash = hiveAndPath.IndexOf('\\');
        string hiveString = firstSlash > 0 ? hiveAndPath[..firstSlash] : hiveAndPath;
        path = firstSlash > 0 ? hiveAndPath[(firstSlash + 1)..] : string.Empty;

        hive = hiveString.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            "HKU" or "HKEY_USERS" => RegistryHive.Users,
            "HKCR" or "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
            _ => RegistryHive.CurrentUser
        };

        return !string.IsNullOrWhiteSpace(valueName);
    }

    private static bool TryParseServiceCondition(string value, out string? serviceName, out string? desiredState)
    {
        int equalsIndex = value.IndexOf('=');
        if (equalsIndex <= 0 || equalsIndex >= value.Length - 1)
        {
            serviceName = null;
            desiredState = null;
            return false;
        }

        serviceName = value[..equalsIndex];
        desiredState = value[(equalsIndex + 1)..];
        return !string.IsNullOrWhiteSpace(serviceName) && !string.IsNullOrWhiteSpace(desiredState);
    }

    private static ServiceControllerStatus ParseServiceState(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "running" => ServiceControllerStatus.Running,
            "stopped" => ServiceControllerStatus.Stopped,
            "paused" => ServiceControllerStatus.Paused,
            _ => ServiceControllerStatus.Running
        };
    }

    [Flags]
    private enum ProcessAccess : uint
    {
        QueryLimitedInformation = 0x1000,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(ProcessAccess desiredAccess, bool inheritHandle, uint processId);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [Flags]
    private enum ToolhelpSnapshotFlags : uint
    {
        Process = 0x00000002,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeSnapshotHandle CreateToolhelp32Snapshot(ToolhelpSnapshotFlags flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32First(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32Next(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);

    private sealed class SafeSnapshotHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeSnapshotHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
