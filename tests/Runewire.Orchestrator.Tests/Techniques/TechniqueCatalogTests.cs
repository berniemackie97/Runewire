using Runewire.Domain.Techniques;
using Runewire.Orchestrator.Techniques;

namespace Runewire.Orchestrator.Tests.Techniques;

public sealed class TechniqueCatalogTests
{
    [Fact]
    public void Ctor_throws_when_registry_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => new TechniqueCatalog(null!));
    }

    [Fact]
    public void GetAll_returns_techniques_sorted_by_name_case_insensitive()
    {
        // Arrange - unsorted input
        FakeRegistry registry = new(
        [
            new InjectionTechniqueDescriptor(
                InjectionTechniqueId.CreateRemoteThread,
                name: "CreateRemoteThread",
                displayName: "CRT",
                category: "User-mode",
                description: "desc",
                requiresKernelMode: false),
            new InjectionTechniqueDescriptor(
                InjectionTechniqueId.Unknown,
                name: "apc",
                displayName: "APC",
                category: "User-mode",
                description: "desc",
                requiresKernelMode: false),
        ]);

        TechniqueCatalog catalog = new(registry);

        // Act
        IReadOnlyList<InjectionTechniqueDescriptor> results = catalog.GetAll();

        // Assert - ordered by name ignoring case: APC before CreateRemoteThread
        Assert.Collection(results, first => Assert.Equal("apc", first.Name), second => Assert.Equal("CreateRemoteThread", second.Name));
    }

    private sealed class FakeRegistry(IReadOnlyList<InjectionTechniqueDescriptor> descriptors) : IInjectionTechniqueRegistry
    {
        public IEnumerable<InjectionTechniqueDescriptor> GetAll() => descriptors;

        public InjectionTechniqueDescriptor? GetById(InjectionTechniqueId id) => descriptors.FirstOrDefault(d => d.Id == id);

        public InjectionTechniqueDescriptor? GetByName(string name) => descriptors.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
