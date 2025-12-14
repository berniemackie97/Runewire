using System.Runtime.InteropServices;

namespace Runewire.Orchestrator.Infrastructure.NativeInterop;

/// <summary>
/// Managed mirror of the native rw_target_kind enum.
/// </summary>
internal enum RwTargetKind : int
{
    Self = 0,
    ProcessId = 1,
    ProcessName = 2,
}

/// <summary>
/// Managed mirror of the native rw_target struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RwTarget
{
    public RwTargetKind Kind;
    public uint Pid;
    public IntPtr ProcessName;
}

/// <summary>
/// Managed mirror of the native rw_injection_request struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RwInjectionRequest
{
    public IntPtr RecipeName;
    public IntPtr RecipeDescription;
    public RwTarget Target;
    public IntPtr TechniqueName;
    public IntPtr PayloadPath;
    public int AllowKernelDrivers;
    public int RequireInteractiveConsent;
}

/// <summary>
/// Managed mirror of the native rw_injection_result struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RwInjectionResult
{
    public int Success;
    public IntPtr ErrorCode;
    public IntPtr ErrorMessage;
    public ulong StartedAtUtcMs;
    public ulong CompletedAtUtcMs;
}
