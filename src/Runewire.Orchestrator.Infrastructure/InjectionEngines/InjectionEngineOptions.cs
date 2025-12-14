namespace Runewire.Orchestrator.Infrastructure.InjectionEngines;

/// <summary>
/// Options for creating injection engines.
/// </summary>
public sealed record InjectionEngineOptions(TextWriter? DryRunOutput);
