using Runewire.Domain.Techniques;
using System.Collections.Immutable;

namespace Runewire.Core.Infrastructure.Techniques;

/// <summary>
/// Built in technique registry.
/// Just an in memory list for now.
///
/// I keep it deterministic:
/// - fixed set at construction
/// - name lookup is case insensitive
/// </summary>
public sealed class BuiltInInjectionTechniqueRegistry : IInjectionTechniqueRegistry
{
    private readonly ImmutableDictionary<InjectionTechniqueId, InjectionTechniqueDescriptor> _byId;
    private readonly ImmutableDictionary<string, InjectionTechniqueDescriptor> _byName;

    public BuiltInInjectionTechniqueRegistry()
    {
        // Seed a couple techniques to start.
        // This list grows as the native injector grows.
        InjectionTechniqueDescriptor[] techniques =
        [
            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.CreateRemoteThread,
                name: "CreateRemoteThread",
                displayName: "CreateRemoteThread DLL Injection",
                category: "User-mode DLL injection",
                description: "Injects a DLL into a target process and starts execution using CreateRemoteThread (or equivalent).",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.QueueUserApc,
                name: "QueueUserAPC",
                displayName: "QueueUserAPC DLL Injection",
                category: "User-mode DLL injection",
                description: "Injects a DLL and schedules its execution via QueueUserAPC on a target thread.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetThreadId", "Target thread id (optional).", required: false, dataType: "int"),
                    new TechniqueParameter("timeoutMs", "Optional APC wait timeout in milliseconds.", required: false, dataType: "int")
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.NtCreateThreadEx,
                name: "NtCreateThreadEx",
                displayName: "NtCreateThreadEx DLL Injection",
                category: "User-mode DLL injection",
                description: "Injects a DLL using the lower-level NtCreateThreadEx syscall for thread creation.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("creationFlags", "Thread creation flags (optional).", required: false, dataType: "int"),
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ManualMap,
                name: "ManualMap",
                displayName: "Manual Map DLL Injection",
                category: "User-mode DLL injection",
                description: "Maps a DLL into a target process without calling LoadLibrary (manual mapping).",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("entryPoint", "Optional override entry point export.", required: false),
                    new TechniqueParameter("ignoreTls", "Skip TLS callbacks.", required: false, dataType: "bool")
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.Shellcode,
                name: "Shellcode",
                displayName: "Shellcode Injection",
                category: "User-mode code injection",
                description: "Injects and executes position-independent shellcode in a target process.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("arch", "Shellcode architecture hint (x86/x64/arm64).", required: false),
                    new TechniqueParameter("entryOffset", "Optional entry offset into shellcode.", required: false, dataType: "int")
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ThreadHijack,
                name: "ThreadHijack",
                displayName: "Thread Hijack Injection",
                category: "Thread hijack",
                description: "Suspends or hijacks an existing thread, writes payload, adjusts context, and resumes to execute.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetThreadId", "Target thread id to hijack.", required: false, dataType: "int")
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.EarlyBirdApc,
                name: "EarlyBirdApc",
                displayName: "Early Bird APC Injection",
                category: "APC injection",
                description: "Queues an APC on a thread before it runs user code (early-bird) to execute payload.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetThreadId", "Target thread id to queue APC on.", required: false, dataType: "int")
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ProcessHollowing,
                name: "ProcessHollowing",
                displayName: "Process Hollowing",
                category: "Process replacement",
                description: "Creates a suspended process, replaces its image with payload, and resumes.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetImagePath", "Path to the target image to hollow.", required: false),
                    new TechniqueParameter("commandLine", "Optional command line for the hollowed process.", required: false)
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ProcessDoppelganging,
                name: "ProcessDoppelganging",
                displayName: "Process Doppelganging",
                category: "Process replacement",
                description: "Uses transacted sections to run a replaced image without touching disk.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetImagePath", "Path to the target image to replace.", required: false),
                ],
                implemented: true,
                requiresDriver: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ProcessHerpaderping,
                name: "ProcessHerpaderping",
                displayName: "Process Herpaderping",
                category: "Process replacement",
                description: "Manipulates on-disk image during creation to obscure payload origin.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetImagePath", "Path to the image to disguise.", required: false),
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ModuleStomping,
                name: "ModuleStomping",
                displayName: "Module Stomping",
                category: "Image reuse",
                description: "Reuses an existing module mapping, overwrites it with payload, and executes.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("moduleName", "Module to stomp (e.g., existing DLL name).", required: false)
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.SharedSectionMap,
                name: "SharedSectionMap",
                displayName: "Shared Section Mapping",
                category: "Section-based injection",
                description: "Delivers payload via shared sections (MapViewOfSection or similar).",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("sectionName", "Optional named section.", required: false),
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ReflectiveDll,
                name: "ReflectiveDll",
                displayName: "Reflective DLL Injection",
                category: "Reflective loaders",
                description: "Loads a DLL reflectively (RDI/sRDI/manual map style) without LoadLibrary.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("exportName", "Export to invoke after loading.", required: false),
                    new TechniqueParameter("argument", "Optional argument for export.", required: false)
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ClrHost,
                name: "ClrHost",
                displayName: "CLR Host Injection",
                category: "Managed code",
                description: "Hosts the CLR in a target process and loads managed payloads.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("assemblyPath", "Path to managed assembly to load.", required: true),
                    new TechniqueParameter("typeName", "Fully qualified type name.", required: true),
                    new TechniqueParameter("methodName", "Static method entry point.", required: true),
                    new TechniqueParameter("argument", "Optional argument to entry point.", required: false)
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.PtraceInject,
                name: "PtraceInject",
                displayName: "ptrace Injection",
                category: "User-mode injection",
                description: "Uses ptrace to write/execute payload in a target process.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Linux },
                parameters:
                [
                    new TechniqueParameter("pid", "Target process id.", required: true, dataType: "int"),
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.MemfdShellcode,
                name: "MemfdShellcode",
                displayName: "memfd Shellcode",
                category: "User-mode injection",
                description: "Uses memfd to stage shellcode or shared object in memory and execute it.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Linux },
                parameters:
                [
                    new TechniqueParameter("loader", "Loader mode (shellcode/so).", required: false),
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.MachThreadInject,
                name: "MachThreadInject",
                displayName: "Mach Thread Injection",
                category: "User-mode injection",
                description: "Uses Mach APIs to create or hijack threads and execute payloads on macOS.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.MacOS },
                parameters:
                [
                    new TechniqueParameter("threadState", "Optional thread state preset.", required: false),
                ],
                implemented: true
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.InlineHook,
                name: "InlineHook",
                displayName: "Inline Hook",
                category: "Hooks and redirects",
                description: "Installs a trampoline/inline hook for a target function in the process.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("moduleName", "Module containing the target function.", required: true),
                    new TechniqueParameter("functionName", "Target function to hook.", required: true),
                    new TechniqueParameter("hookPayload", "Path or identifier of hook payload/handler.", required: false),
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.IatHook,
                name: "IatHook",
                displayName: "IAT Hook",
                category: "Hooks and redirects",
                description: "Patches the Import Address Table to redirect a target import to a hook handler.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("moduleName", "Module whose IAT entry will be patched.", required: true),
                    new TechniqueParameter("importName", "Imported function name to redirect.", required: true),
                    new TechniqueParameter("hookPayload", "Path or identifier of hook payload/handler.", required: false),
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.EatHook,
                name: "EatHook",
                displayName: "EAT Hook",
                category: "Hooks and redirects",
                description: "Patches the Export Address Table to redirect callers to a hook handler.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("moduleName", "Module whose export will be patched.", required: true),
                    new TechniqueParameter("exportName", "Exported function name to redirect.", required: true),
                    new TechniqueParameter("hookPayload", "Path or identifier of hook payload/handler.", required: false),
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.WinsockRedirect,
                name: "WinsockRedirect",
                displayName: "Winsock Redirect",
                category: "Hooks and redirects",
                description: "Redirects Winsock calls (connect/send/recv) to a specified endpoint or handler.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetHost", "Host to redirect to.", required: true),
                    new TechniqueParameter("targetPort", "Port to redirect to.", required: true, dataType: "int"),
                    new TechniqueParameter("mode", "Redirect mode (inline/hook/patch).", required: false),
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.HttpRedirect,
                name: "HttpRedirect",
                displayName: "HTTP Redirect",
                category: "Hooks and redirects",
                description: "Redirects WinHTTP/WinINet outbound requests to an alternate endpoint.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetUrl", "URL to redirect to.", required: true),
                    new TechniqueParameter("scope", "API scope (WinHTTP/WinINet).", required: false)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.DnsOverride,
                name: "DnsOverride",
                displayName: "DNS Override",
                category: "Hooks and redirects",
                description: "Overrides DNS resolution for specific hostnames.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("host", "Hostname to override.", required: true),
                    new TechniqueParameter("address", "Replacement IP address.", required: true)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.FileSystemRedirect,
                name: "FileSystemRedirect",
                displayName: "File System Redirect",
                category: "Hooks and redirects",
                description: "Redirects file system operations to alternate paths (e.g., sandbox).",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetPath", "Path or prefix to intercept.", required: true),
                    new TechniqueParameter("redirectPath", "Destination path to redirect to.", required: true)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.TlsBypass,
                name: "TlsBypass",
                displayName: "TLS Bypass",
                category: "Hooks and redirects",
                description: "Bypasses or logs TLS validation in user-mode stacks.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("scope", "TLS stack (SChannel/WinHTTP/WinINet).", required: false)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.EarlyBirdCreateProcess,
                name: "EarlyBirdCreateProcess",
                displayName: "Early Bird CreateProcess",
                category: "Process launch",
                description: "Creates a process in a suspended state and injects before main runs (early bird).",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("commandLine", "Command line to launch.", required: true)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.EarlyBirdQueueApc,
                name: "EarlyBirdQueueApc",
                displayName: "Early Bird QueueUserAPC",
                category: "APC injection",
                description: "Queues an APC before main runs during process creation.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("commandLine", "Command line to launch.", required: true)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.SectionCopyExecute,
                name: "SectionCopyExecute",
                displayName: "Section Copy Execute",
                category: "Section-based injection",
                description: "Copies payload into RWX memory without mapping a section and executes.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("entryOffset", "Optional entry offset.", required: false, dataType: "int")
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ThreadpoolApc,
                name: "ThreadpoolApc",
                displayName: "Threadpool APC",
                category: "APC injection",
                description: "Hijacks threadpool worker threads to execute payload.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("targetThreadId", "Specific thread ID to target (optional).", required: false, dataType: "int")
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.ModuleStompRestore,
                name: "ModuleStompRestore",
                displayName: "Module Stomp with Restore",
                category: "Image reuse",
                description: "Overwrites a module for execution and restores it afterward.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("moduleName", "Module to stomp/restore.", required: true)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.CallExportInit,
                name: "CallExportInit",
                displayName: "Call Export Init",
                category: "Payload control",
                description: "Calls a specified export in the payload with parameters after injection.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("exportName", "Exported function to call.", required: true),
                    new TechniqueParameter("argument", "Optional argument string.", required: false)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.LdPreloadLaunch,
                name: "LdPreloadLaunch",
                displayName: "LD_PRELOAD Launch",
                category: "Process launch",
                description: "Launches a process with LD_PRELOAD set to inject a shared object.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Linux },
                parameters:
                [
                    new TechniqueParameter("libraryPath", "Shared object to preload.", required: true),
                    new TechniqueParameter("commandLine", "Command line to launch.", required: true)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.DyldInsertLaunch,
                name: "DyldInsertLaunch",
                displayName: "DYLD_INSERT_LIBRARIES Launch",
                category: "Process launch",
                description: "Launches a process with DYLD_INSERT_LIBRARIES to inject a dylib.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.MacOS },
                parameters:
                [
                    new TechniqueParameter("libraryPath", "Dylib to insert.", required: true),
                    new TechniqueParameter("commandLine", "Command line to launch.", required: true)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.PtraceThreadHijack,
                name: "PtraceThreadHijack",
                displayName: "ptrace Thread Hijack",
                category: "User-mode injection",
                description: "Uses ptrace to hijack a thread and execute payload.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Linux },
                parameters:
                [
                    new TechniqueParameter("pid", "Target process id.", required: true, dataType: "int")
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.MemoryScanPatch,
                name: "MemoryScanPatch",
                displayName: "Memory Scan/Patch",
                category: "Diagnostics",
                description: "Scans memory for a pattern and optionally patches it.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("pattern", "Pattern to search for.", required: true),
                    new TechniqueParameter("replacement", "Replacement bytes (optional).", required: false)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.AntiHookDetect,
                name: "AntiHookDetect",
                displayName: "Anti-Hook Detection",
                category: "Diagnostics",
                description: "Detects common user-mode hooks for analysis.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("moduleName", "Module to scan (optional).", required: false)
                ],
                implemented: false
            ),

            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.SnapshotRestore,
                name: "SnapshotRestore",
                displayName: "Snapshot/Restore",
                category: "Diagnostics",
                description: "Captures and restores original bytes/sections to undo patches.",
                requiresKernelMode: false,
                platforms: new[] { TechniquePlatform.Windows },
                parameters:
                [
                    new TechniqueParameter("snapshotName", "Snapshot label (optional).", required: false)
                ],
                implemented: false
            ),
        ];

        _byId = techniques.ToImmutableDictionary(t => t.Id);

        // Case insensitive so recipe authors do not have to care about casing.
        _byName = techniques.ToImmutableDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<InjectionTechniqueDescriptor> GetAll() => _byId.Values;

    public InjectionTechniqueDescriptor? GetById(InjectionTechniqueId id)
    {
        return _byId.TryGetValue(id, out InjectionTechniqueDescriptor? descriptor) ? descriptor : null;
    }

    public InjectionTechniqueDescriptor? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _byName.TryGetValue(name, out InjectionTechniqueDescriptor? descriptor) ? descriptor : null;
    }
}
