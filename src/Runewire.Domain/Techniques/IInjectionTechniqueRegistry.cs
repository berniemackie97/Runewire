namespace Runewire.Domain.Techniques;

/// <summary>
/// Readonly registry of injection techniques.
/// I use this for listing techniques, validating recipe technique names, and driving UI/docs.
/// </summary>
public interface IInjectionTechniqueRegistry
{
    /// <summary>
    /// All techniques in this registry.
    /// </summary>
    IEnumerable<InjectionTechniqueDescriptor> GetAll();

    /// <summary>
    /// Lookup by stable technique id.
    /// </summary>
    InjectionTechniqueDescriptor? GetById(InjectionTechniqueId id);

    /// <summary>
    /// Lookup by canonical name (case insensitive).
    /// </summary>
    InjectionTechniqueDescriptor? GetByName(string name);
}
