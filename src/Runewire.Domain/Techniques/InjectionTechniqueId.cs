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

    /// <summary>
    /// Inline/trampoline hook for a specific function.
    /// </summary>
    InlineHook = 18,

    /// <summary>
    /// Import Address Table hook.
    /// </summary>
    IatHook = 19,

    /// <summary>
    /// Export Address Table hook.
    /// </summary>
    EatHook = 20,

    /// <summary>
    /// Winsock redirect/shim.
    /// </summary>
    WinsockRedirect = 21,

    /// <summary>
    /// HTTP/HTTPS redirect shim (WinINet/WinHTTP).
    /// </summary>
    HttpRedirect = 22,

    /// <summary>
    /// DNS override shim.
    /// </summary>
    DnsOverride = 23,

    /// <summary>
    /// File system redirect shim.
    /// </summary>
    FileSystemRedirect = 24,

    /// <summary>
    /// TLS bypass shim.
    /// </summary>
    TlsBypass = 25,

    /// <summary>
    /// Early bird CreateProcess/entry hijack.
    /// </summary>
    EarlyBirdCreateProcess = 26,

    /// <summary>
    /// Early bird QueueUserAPC before main.
    /// </summary>
    EarlyBirdQueueApc = 27,

    /// <summary>
    /// Section copy and execute without mapping.
    /// </summary>
    SectionCopyExecute = 28,

    /// <summary>
    /// Threadpool APC hijack.
    /// </summary>
    ThreadpoolApc = 29,

    /// <summary>
    /// Module stomp with restore capability.
    /// </summary>
    ModuleStompRestore = 30,

    /// <summary>
    /// Call exported init on payload with parameters.
    /// </summary>
    CallExportInit = 31,

    /// <summary>
    /// Linux LD_PRELOAD style launch injection.
    /// </summary>
    LdPreloadLaunch = 32,

    /// <summary>
    /// macOS DYLD_INSERT_LIBRARIES launch injection.
    /// </summary>
    DyldInsertLaunch = 33,

    /// <summary>
    /// ptrace-based thread hijack.
    /// </summary>
    PtraceThreadHijack = 34,

    /// <summary>
    /// Memory scan and optional patch.
    /// </summary>
    MemoryScanPatch = 35,

    /// <summary>
    /// Anti-hook detector.
    /// </summary>
    AntiHookDetect = 36,

    /// <summary>
    /// Snapshot and restore original bytes/sections.
    /// </summary>
    SnapshotRestore = 37,
}
