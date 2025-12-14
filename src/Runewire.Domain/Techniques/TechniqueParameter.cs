namespace Runewire.Domain.Techniques;

/// <summary>
/// Parameter metadata for a technique.
/// </summary>
public sealed class TechniqueParameter
{
    public TechniqueParameter(string name, string description, bool required = true, string? dataType = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Parameter name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Parameter description is required.", nameof(description));
        }

        Name = name;
        Description = description;
        Required = required;
        DataType = dataType ?? "string";
    }

    public string Name { get; }

    public string Description { get; }

    public bool Required { get; }

    public string DataType { get; }
}
