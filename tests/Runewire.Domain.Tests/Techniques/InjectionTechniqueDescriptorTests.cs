using Runewire.Domain.Techniques;

namespace Runewire.Domain.Tests.Techniques;

/// <summary>
/// Unit tests for <see cref="InjectionTechniqueDescriptor"/> to ensure
/// its invariants are enforced consistently.
/// </summary>
public sealed class InjectionTechniqueDescriptorTests
{
    [Fact]
    public void Ctor_sets_properties_when_all_arguments_are_valid()
    {
        // Arrange
        InjectionTechniqueId id = InjectionTechniqueId.CreateRemoteThread;

        // Act
        InjectionTechniqueDescriptor descriptor = new(
            id: id,
            name: "CreateRemoteThread",
            displayName: "CreateRemoteThread DLL Injection",
            category: "User-mode DLL injection",
            description: "Injects a DLL into a target process.",
            requiresKernelMode: false);

        // Assert
        Assert.Equal(id, descriptor.Id);
        Assert.Equal("CreateRemoteThread", descriptor.Name);
        Assert.Equal("CreateRemoteThread DLL Injection", descriptor.DisplayName);
        Assert.Equal("User-mode DLL injection", descriptor.Category);
        Assert.Equal("Injects a DLL into a target process.", descriptor.Description);
        Assert.False(descriptor.RequiresKernelMode);
    }

    [Fact]
    public void Ctor_throws_when_name_is_missing()
    {
        Assert.Throws<ArgumentException>(() => new InjectionTechniqueDescriptor(
                InjectionTechniqueId.Unknown,
                name: "",
                displayName: "Display",
                category: "Category",
                description: "Description",
                requiresKernelMode: false));
    }

    [Fact]
    public void Ctor_throws_when_display_name_is_missing()
    {
        Assert.Throws<ArgumentException>(() => new InjectionTechniqueDescriptor(
                InjectionTechniqueId.Unknown,
                name: "Name",
                displayName: "",
                category: "Category",
                description: "Description",
                requiresKernelMode: false));
    }

    [Fact]
    public void Ctor_throws_when_category_is_missing()
    {
        Assert.Throws<ArgumentException>(() => new InjectionTechniqueDescriptor(
                InjectionTechniqueId.Unknown,
                name: "Name",
                displayName: "Display",
                category: "",
                description: "Description",
                requiresKernelMode: false));
    }

    [Fact]
    public void Ctor_throws_when_description_is_missing()
    {
        Assert.Throws<ArgumentException>(() => new InjectionTechniqueDescriptor(
                InjectionTechniqueId.Unknown,
                name: "Name",
                displayName: "Display",
                category: "Category",
                description: "",
                requiresKernelMode: false));
    }
}
