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
        IEnumerable<string>? requiredParameters = null)
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

        List<string> parameterList = requiredParameters?.ToList() ?? [];
        if (parameterList.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Required parameters cannot be empty.", nameof(requiredParameters));
        }

        Id = id;
        Name = name;
        DisplayName = displayName;
        Category = category;
        Description = description;
        RequiresKernelMode = requiresKernelMode;
        Platforms = platformList;
        RequiredParameters = parameterList;
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
    /// Parameters that must be present in the recipe technique section.
    /// </summary>
    public IReadOnlyList<string> RequiredParameters { get; }
}
