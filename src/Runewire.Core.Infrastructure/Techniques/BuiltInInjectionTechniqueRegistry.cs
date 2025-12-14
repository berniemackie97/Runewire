using System;
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
                    new TechniqueParameter("targetThreadId", "Target thread id (optional).", required: false, dataType: "int")
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
