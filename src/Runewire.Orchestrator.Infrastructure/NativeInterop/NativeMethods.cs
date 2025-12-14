using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Runewire.Orchestrator.Infrastructure.NativeInterop;

/// <summary>
/// P/Invoke surface for the native injector.
/// </summary>
internal static partial class NativeMethods
{
    // Base name so the runtime can resolve the platform-specific file later.
    private const string DllName = "Runewire.Injector";

    /// <summary>
    /// rw_inject entrypoint.
    /// </summary>
    [LibraryImport(DllName, EntryPoint = "rw_inject")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int RwInject(in RwInjectionRequest request, out RwInjectionResult result);
}
