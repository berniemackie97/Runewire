using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.InjectionEngines;

/// <summary>
/// Default engine factory.
/// </summary>
public sealed class InjectionEngineFactory : IInjectionEngineFactory
{
    public IInjectionEngine Create(bool useNativeEngine, InjectionEngineOptions? options = null)
    {
        if (useNativeEngine)
        {
            return new NativeInjectionEngine();
        }

        TextWriter output = options?.DryRunOutput ?? Console.Out;
        return new DryRunInjectionEngine(output);
    }
}
