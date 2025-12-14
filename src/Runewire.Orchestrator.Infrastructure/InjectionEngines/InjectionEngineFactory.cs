using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.InjectionEngines;

/// <summary>
/// Default engine factory.
/// </summary>
public sealed class InjectionEngineFactory : IInjectionEngineFactory
{
    public IInjectionEngine Create(bool useNativeEngine) => useNativeEngine ? new NativeInjectionEngine() : new DryRunInjectionEngine();
}
