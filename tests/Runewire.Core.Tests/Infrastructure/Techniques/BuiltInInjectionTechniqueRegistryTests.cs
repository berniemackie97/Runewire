using Runewire.Core.Infrastructure.Techniques;
using Runewire.Domain.Techniques;

namespace Runewire.Core.Tests.Infrastructure.Techniques;

/// <summary>
/// Tests for the built in injection technique registry implementation.
/// These tests lock in the default technique catalog used by the rest
/// of the platform.
/// </summary>
public sealed class BuiltInInjectionTechniqueRegistryTests
{
    private readonly BuiltInInjectionTechniqueRegistry _registry = new();

    [Fact]
    public void GetAll_contains_CreateRemoteThread_technique_with_expected_metadata()
    {
        // Act
        InjectionTechniqueDescriptor createRemoteThread = Assert.Single(_registry.GetAll(), t => t.Id == InjectionTechniqueId.CreateRemoteThread);

        // Assert
        Assert.Equal("CreateRemoteThread", createRemoteThread.Name);
        Assert.Equal("CreateRemoteThread DLL Injection", createRemoteThread.DisplayName);
        Assert.Equal("User-mode DLL injection", createRemoteThread.Category);
        Assert.False(string.IsNullOrWhiteSpace(createRemoteThread.Description));
        Assert.False(createRemoteThread.RequiresKernelMode);
        Assert.Contains(TechniquePlatform.Windows, createRemoteThread.Platforms);
    }

    [Fact]
    public void Registry_contains_expected_techniques()
    {
        InjectionTechniqueDescriptor? crt = _registry.GetByName("CreateRemoteThread");
        InjectionTechniqueDescriptor? apc = _registry.GetByName("QueueUserAPC");
        InjectionTechniqueDescriptor? nt = _registry.GetByName("NtCreateThreadEx");
        InjectionTechniqueDescriptor? manualMap = _registry.GetByName("ManualMap");
        InjectionTechniqueDescriptor? shellcode = _registry.GetByName("Shellcode");

        Assert.NotNull(crt);
        Assert.NotNull(apc);
        Assert.NotNull(nt);
        Assert.NotNull(manualMap);
        Assert.NotNull(shellcode);
    }

    [Fact]
    public void GetById_and_GetByName_return_same_descriptor_instance()
    {
        // Act
        InjectionTechniqueDescriptor? byId = _registry.GetById(InjectionTechniqueId.CreateRemoteThread);
        InjectionTechniqueDescriptor? byName = _registry.GetByName("CreateRemoteThread");

        // Assert
        Assert.NotNull(byId);
        Assert.NotNull(byName);
        Assert.Same(byId, byName);
    }

    [Fact]
    public void GetByName_is_case_insensitive_and_null_safe()
    {
        // Act
        InjectionTechniqueDescriptor? upper = _registry.GetByName("CREATEREMOTETHREAD");
        InjectionTechniqueDescriptor? mixed = _registry.GetByName("CreateRemoteThread");
        InjectionTechniqueDescriptor? lower = _registry.GetByName("createremotethread");

        // Assert
        Assert.NotNull(upper);
        Assert.NotNull(mixed);
        Assert.NotNull(lower);

        Assert.Same(upper, mixed);
        Assert.Same(mixed, lower);

        // Null/empty cases are simply treated as "not found".
        Assert.Null(_registry.GetByName(string.Empty));
        Assert.Null(_registry.GetByName(null!));
        Assert.Null(_registry.GetByName("DoesNotExist"));
    }

    [Fact]
    public void Registry_contains_core_technique_set_with_expected_platforms()
    {
        (string Name, TechniquePlatform Platform)[] expected =
        [
            ("CreateRemoteThread", TechniquePlatform.Windows),
            ("QueueUserAPC", TechniquePlatform.Windows),
            ("NtCreateThreadEx", TechniquePlatform.Windows),
            ("ManualMap", TechniquePlatform.Windows),
            ("Shellcode", TechniquePlatform.Windows),
            ("ThreadHijack", TechniquePlatform.Windows),
            ("EarlyBirdApc", TechniquePlatform.Windows),
            ("ProcessHollowing", TechniquePlatform.Windows),
            ("ProcessDoppelganging", TechniquePlatform.Windows),
            ("ProcessHerpaderping", TechniquePlatform.Windows),
            ("ModuleStomping", TechniquePlatform.Windows),
            ("SharedSectionMap", TechniquePlatform.Windows),
            ("ReflectiveDll", TechniquePlatform.Windows),
            ("ClrHost", TechniquePlatform.Windows),
            ("PtraceInject", TechniquePlatform.Linux),
            ("MemfdShellcode", TechniquePlatform.Linux),
            ("MachThreadInject", TechniquePlatform.MacOS),
        ];

        foreach ((string name, TechniquePlatform platform) in expected)
        {
            InjectionTechniqueDescriptor? descriptor = _registry.GetByName(name);
            Assert.NotNull(descriptor);
            Assert.Contains(platform, descriptor!.Platforms);
            Assert.True(descriptor.Implemented);
        }
    }
}
