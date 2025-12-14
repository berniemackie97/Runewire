namespace Runewire.Domain.Techniques;

/// <summary>
/// Metadata for an injection technique supported by Runewire.
/// </summary>
public sealed class InjectionTechniqueDescriptor
{
    public InjectionTechniqueDescriptor(
        InjectionTechniqueId id,
        string name,
        string displayName,
        string category,
        string description,
        bool requiresKernelMode,
        IEnumerable<TechniquePlatform>? platforms,
        IEnumerable<TechniqueParameter>? parameters = null,
        bool implemented = false,
        bool requiresDriver = false,
        string? minNativeVersion = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Technique name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        List<TechniquePlatform> platformList = platforms?.ToList() ?? [];
        if (platformList.Count == 0)
        {
            throw new ArgumentException("At least one supported platform is required.", nameof(platforms));
        }

        List<TechniqueParameter> parameterList = parameters?.ToList() ?? [];
        if (parameterList.Any(p => p is null))
        {
            throw new ArgumentException("Technique parameters cannot contain null.", nameof(parameters));
        }

        Id = id;
        Name = name;
        DisplayName = displayName;
        Category = category;
        Description = description;
        RequiresKernelMode = requiresKernelMode;
        Platforms = platformList;
        Parameters = parameterList;
        Implemented = implemented;
        RequiresDriver = requiresDriver;
        MinNativeVersion = string.IsNullOrWhiteSpace(minNativeVersion) ? null : minNativeVersion;
    }

    /// <summary>
    /// Stable id for this technique.
    /// </summary>
    public InjectionTechniqueId Id { get; }

    /// <summary>
    /// Canonical name used in recipes (example: CreateRemoteThread).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Display name for UI and docs.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Category label for grouping in UI and docs.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Short description of what the technique does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// True if this technique needs kernel mode capability.
    /// </summary>
    public bool RequiresKernelMode { get; }

    /// <summary>
    /// Platforms this technique supports.
    /// </summary>
    public IReadOnlyList<TechniquePlatform> Platforms { get; }

    /// <summary>
    /// Parameters used by this technique.
    /// </summary>
    public IReadOnlyList<TechniqueParameter> Parameters { get; }

    /// <summary>
    /// True if the native engine supports this technique in this build.
    /// </summary>
    public bool Implemented { get; }

    /// <summary>
    /// True if this technique depends on a kernel driver or elevated component.
    /// </summary>
    public bool RequiresDriver { get; }

    /// <summary>
    /// Minimum native engine version required, if any.
    /// </summary>
    public string? MinNativeVersion { get; }
}
