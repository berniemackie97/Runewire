using System.Diagnostics.CodeAnalysis;

namespace Runewire.Orchestrator.Infrastructure.NativeInterop;

/// <summary>
/// Wrapper around the native rw_inject entrypoint.
/// I keep this as an interface so I can fake it in tests without loading the native DLL.
/// </summary>
internal interface INativeInjectorInvoker
{
    /// <summary>
    /// Call the native injector.
    /// Caller owns the lifetime of any unmanaged memory referenced by the request.
    /// </summary>
    int Inject(in RwInjectionRequest request, out RwInjectionResult result);
}

/// <summary>
/// Default invoker that forwards to the P/Invoke layer.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class NativeInjectorInvoker : INativeInjectorInvoker
{
    public int Inject(in RwInjectionRequest request, out RwInjectionResult result) => NativeMethods.RwInject(in request, out result);
}
