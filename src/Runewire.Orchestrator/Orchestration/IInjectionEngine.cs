namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Injection runtime abstraction.
/// </summary>
public interface IInjectionEngine
{
    /// <summary>
    /// Execute an injection request and return an InjectionResult.
    /// Expected failures should come back as a failed result, not exceptions.
    /// Exceptions are for actual broken stuff.
    /// </summary>
    Task<InjectionResult> ExecuteAsync(InjectionRequest request, CancellationToken cancellationToken = default);
}
