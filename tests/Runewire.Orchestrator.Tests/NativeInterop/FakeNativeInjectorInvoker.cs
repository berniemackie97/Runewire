using Runewire.Orchestrator.Infrastructure.NativeInterop;

namespace Runewire.Orchestrator.Tests.NativeInterop;

/// <summary>
/// Test double for <see cref="INativeInjectorInvoker"/> that allows
/// tests to control the native call outcome without loading the real
/// Runewire.Injector library.
/// </summary>
internal sealed class FakeNativeInjectorInvoker : INativeInjectorInvoker
{
    /// <summary>
    /// Function used to produce a result for each invocation.
    /// Tests can override this to simulate various native outcomes.
    /// </summary>
    internal Func<RwInjectionRequest, (int Status, RwInjectionResult Result)> Behavior { get; set; } = DefaultSuccess;

    int INativeInjectorInvoker.Inject(in RwInjectionRequest request, out RwInjectionResult result)
    {
        (int status, RwInjectionResult nativeResult) = Behavior(request);
        result = nativeResult;
        return status;
    }

    private static (int Status, RwInjectionResult Result) DefaultSuccess(RwInjectionRequest _)
    {
        RwInjectionResult result = new()
        {
            Success = 1,
            ErrorCode = IntPtr.Zero,
            ErrorMessage = IntPtr.Zero,
            StartedAtUtcMs = 0,
            CompletedAtUtcMs = 0,
        };

        return (0, result);
    }
}
