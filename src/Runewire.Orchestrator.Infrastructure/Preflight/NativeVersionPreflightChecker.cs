using Runewire.Core.Infrastructure.Techniques;
using Runewire.Domain.Techniques;
using Runewire.Domain.Validation;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Compares required technique native versions to the current injector version.
/// </summary>
public sealed class NativeVersionPreflightChecker
{
    private readonly INativeVersionProvider _versionProvider;
    private readonly BuiltInInjectionTechniqueRegistry _registry;

    public NativeVersionPreflightChecker(INativeVersionProvider versionProvider, BuiltInInjectionTechniqueRegistry registry)
    {
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IReadOnlyList<RecipeValidationError> Check(string techniqueName)
    {
        InjectionTechniqueDescriptor? descriptor = _registry.GetByName(techniqueName);
        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.MinNativeVersion))
        {
            return Array.Empty<RecipeValidationError>();
        }

        Version? current = _versionProvider.CurrentNativeVersion;
        if (current is null)
        {
            return new[] { new RecipeValidationError("NATIVE_VERSION_UNKNOWN", "Native injector version could not be determined.") };
        }

        if (Version.TryParse(descriptor.MinNativeVersion, out Version? required) && required is not null)
        {
            if (current < required)
            {
                return new[] { new RecipeValidationError("NATIVE_VERSION_TOO_OLD", $"Technique '{techniqueName}' requires native injector version {required} or later (found {current}).") };
            }
        }

        return Array.Empty<RecipeValidationError>();
    }
}
