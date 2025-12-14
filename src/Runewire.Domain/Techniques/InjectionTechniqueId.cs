namespace Runewire.Domain.Techniques;

/// <summary>
/// Stable ids for built in techniques.
/// </summary>
public enum InjectionTechniqueId
{
    /// <summary>
    /// Unknown or custom technique id.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// DLL injection via CreateRemoteThread.
    /// </summary>
    CreateRemoteThread = 1,

    /// <summary>
    /// Queue an APC to load a DLL in a target thread.
    /// </summary>
    QueueUserApc = 2,

    /// <summary>
    /// Create a remote thread using NtCreateThreadEx.
    /// </summary>
    NtCreateThreadEx = 3,

    /// <summary>
    /// Manual map a DLL without LoadLibrary.
    /// </summary>
    ManualMap = 4,

    /// <summary>
    /// Inject and execute position-independent shellcode.
    /// </summary>
    Shellcode = 5,

    /// <summary>
    /// Hijack an existing thread via context manipulation.
    /// </summary>
    ThreadHijack = 6,

    /// <summary>
    /// Early bird APC injection on a suspended thread.
    /// </summary>
    EarlyBirdApc = 7,

    /// <summary>
    /// Process hollowing / RunPE.
    /// </summary>
    ProcessHollowing = 8,

    /// <summary>
    /// Process doppelganging via transacted sections.
    /// </summary>
    ProcessDoppelganging = 9,

    /// <summary>
    /// Process herpaderping image replacement.
    /// </summary>
    ProcessHerpaderping = 10,

    /// <summary>
    /// Module stomping to reuse an existing image.
    /// </summary>
    ModuleStomping = 11,

    /// <summary>
    /// Shared section / MapViewOfSection payload delivery.
    /// </summary>
    SharedSectionMap = 12,

    /// <summary>
    /// Reflective DLL injection (RDI/sRDI).
    /// </summary>
    ReflectiveDll = 13,

    /// <summary>
    /// Inject managed code via CLR hosting.
    /// </summary>
    ClrHost = 14,

    /// <summary>
    /// Linux ptrace-based injection.
    /// </summary>
    PtraceInject = 15,

    /// <summary>
    /// Linux memfd shellcode/dll injection.
    /// </summary>
    MemfdShellcode = 16,

    /// <summary>
    /// macOS Mach-O thread creation injection.
    /// </summary>
    MachThreadInject = 17,
}
