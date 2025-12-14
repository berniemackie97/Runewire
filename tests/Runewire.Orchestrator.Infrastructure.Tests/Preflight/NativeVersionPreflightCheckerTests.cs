using Runewire.Core.Infrastructure.Techniques;
using Runewire.Domain.Techniques;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Infrastructure.Preflight;

namespace Runewire.Orchestrator.Infrastructure.Tests.Preflight;

public sealed class NativeVersionPreflightCheckerTests
{
    [Fact]
    public void Check_returns_empty_when_descriptor_missing_or_no_version()
    {
        BuiltInInjectionTechniqueRegistry registry = new();
        FakeProvider provider = new(new Version(1, 0, 0, 0));
        NativeVersionPreflightChecker checker = new(provider, registry);

        IReadOnlyList<RecipeValidationError> result = checker.Check("DoesNotExist");

        Assert.Empty(result);
    }

    [Fact]
    public void Check_returns_error_when_version_unknown()
    {
        BuiltInInjectionTechniqueRegistry registry = new();
        FakeProvider provider = new(null);
        NativeVersionPreflightChecker checker = new(provider, registry);

        IReadOnlyList<RecipeValidationError> result = checker.Check("ProcessDoppelganging"); // has min version set

        Assert.Contains(result, e => e.Code == "NATIVE_VERSION_UNKNOWN");
    }

    [Fact]
    public void Check_returns_error_when_version_too_old()
    {
        BuiltInInjectionTechniqueRegistry registry = new();
        FakeProvider provider = new(new Version(0, 9, 0, 0));
        NativeVersionPreflightChecker checker = new(provider, registry);

        IReadOnlyList<RecipeValidationError> result = checker.Check("ProcessDoppelganging");

        Assert.Contains(result, e => e.Code == "NATIVE_VERSION_TOO_OLD");
    }

    [Fact]
    public void Check_returns_empty_when_version_satisfied()
    {
        BuiltInInjectionTechniqueRegistry registry = new();
        FakeProvider provider = new(new Version(2, 0, 0, 0));
        NativeVersionPreflightChecker checker = new(provider, registry);

        IReadOnlyList<RecipeValidationError> result = checker.Check("ProcessDoppelganging");

        Assert.Empty(result);
    }

    private sealed class FakeProvider(Version? version) : INativeVersionProvider
    {
        public Version? CurrentNativeVersion => version;
    }
}
