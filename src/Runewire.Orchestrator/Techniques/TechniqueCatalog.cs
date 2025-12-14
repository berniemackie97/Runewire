using Runewire.Domain.Techniques;

namespace Runewire.Orchestrator.Techniques;

/// <summary>
/// Read-only view of available injection techniques.
/// </summary>
public sealed class TechniqueCatalog(IInjectionTechniqueRegistry registry)
{
    private readonly IInjectionTechniqueRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    /// <summary>
    /// Get all techniques in a stable, case-insensitive name order.
    /// </summary>
    public IReadOnlyList<InjectionTechniqueDescriptor> GetAll()
    {
        return [.. _registry.GetAll().OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
