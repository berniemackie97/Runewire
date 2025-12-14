using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.InjectionEngines;

/// <summary>
/// Factory for injection engines (native vs dry-run).
/// </summary>
public interface IInjectionEngineFactory
{
    IInjectionEngine Create(bool useNativeEngine);
}
