namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Provides the current native injector version, if available.
/// </summary>
public interface INativeVersionProvider
{
    Version? CurrentNativeVersion { get; }
}
