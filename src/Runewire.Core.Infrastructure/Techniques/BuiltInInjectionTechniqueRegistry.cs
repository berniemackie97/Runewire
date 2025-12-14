using Runewire.Domain.Techniques;
using System.Collections.Immutable;

namespace Runewire.Core.Infrastructure.Techniques;

/// <summary>
/// Built in technique registry.
/// Just an in memory list for now.
///
/// I keep it deterministic:
/// - fixed set at construction
/// - name lookup is case insensitive
/// </summary>
public sealed class BuiltInInjectionTechniqueRegistry : IInjectionTechniqueRegistry
{
    private readonly ImmutableDictionary<InjectionTechniqueId, InjectionTechniqueDescriptor> _byId;
    private readonly ImmutableDictionary<string, InjectionTechniqueDescriptor> _byName;

    public BuiltInInjectionTechniqueRegistry()
    {
        // Seed a couple techniques to start.
        // This list grows as the native injector grows.
        InjectionTechniqueDescriptor[] techniques =
        [
            new InjectionTechniqueDescriptor(
                id: InjectionTechniqueId.CreateRemoteThread,
                name: "CreateRemoteThread",
                displayName: "CreateRemoteThread DLL Injection",
                category: "User-mode DLL injection",
                description: "Injects a DLL into a target process and starts execution using CreateRemoteThread (or equivalent).",
                requiresKernelMode: false
            ),

            // new InjectionTechniqueDescriptor(
            //     InjectionTechniqueId.QueueUserApc,
            //     "QueueUserAPC",
            //     "QueueUserAPC DLL Injection",
            //     "User-mode DLL injection",
            //     "Injects a DLL and schedules its execution via QueueUserAPC.",
            //     requiresKernelMode: false),
        ];

        _byId = techniques.ToImmutableDictionary(t => t.Id);

        // Case insensitive so recipe authors do not have to care about casing.
        _byName = techniques.ToImmutableDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<InjectionTechniqueDescriptor> GetAll() => _byId.Values;

    public InjectionTechniqueDescriptor? GetById(InjectionTechniqueId id)
    {
        return _byId.TryGetValue(id, out InjectionTechniqueDescriptor? descriptor) ? descriptor : null;
    }

    public InjectionTechniqueDescriptor? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _byName.TryGetValue(name, out InjectionTechniqueDescriptor? descriptor) ? descriptor : null;
    }
}
