using System.Diagnostics.CodeAnalysis;

namespace Runewire.Orchestrator.NativeInterop;

/// <summary>
/// Abstraction over the native rw_inject entrypoint.
/// This exists so that the orchestration layer can be tested
/// without loading the actual Runewire.Injector DLL.
/// </summary>
internal interface INativeInjectorInvoker
{
    int Inject(in RwInjectionRequest request, out RwInjectionResult result);
}

/// <summary>
/// Default invoker that calls the P/Invoke layer. direct tests use fakes
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class NativeInjectorInvoker : INativeInjectorInvoker
{
    public int Inject(in RwInjectionRequest request, out RwInjectionResult result) => NativeMethods.RwInject(in request, out result);
}
