using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Loads YAML into a RunewireRecipe, then validates it.
/// I keep parsing, mapping, and validation separated so failures are easier to reason about.
/// </summary>
public sealed class YamlRecipeLoader(IRecipeValidator validator) : IYamlRecipeLoader
{
    // Deserializer config is fixed per loader instance.
    private readonly IDeserializer _deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    private readonly IRecipeValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    /// <summary>
    /// Parse YAML from a string, map it into the domain model, then validate it.
    /// Throw RecipeLoadException when anything fails (parse, structure, or validation).
    /// </summary>
    public RunewireRecipe LoadFromString(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        RecipeDocument? document;

        try
        {
            document = _deserializer.Deserialize<RecipeDocument>(yaml);
        }
        catch (Exception ex)
        {
            throw new RecipeLoadException("Failed to parse recipe YAML.", null, ex);
        }

        if (document is null)
        {
            throw new RecipeLoadException("Recipe YAML content is empty or invalid.");
        }

        RunewireRecipe recipe = MapToDomain(document);

        RecipeValidationResult validationResult = _validator.Validate(recipe);
        if (!validationResult.IsValid)
        {
            throw new RecipeLoadException("Recipe failed validation.", validationResult.Errors);
        }

        return recipe;
    }

    /// <summary>
    /// Read the YAML file and then delegate to LoadFromString so the core logic stays in one place.
    /// </summary>
    public RunewireRecipe LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be null or whitespace.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Recipe file not found.", path);
        }

        string yaml;
        try
        {
            yaml = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            throw new RecipeLoadException($"Failed to read recipe file '{path}'.", null, ex);
        }

        return LoadFromString(yaml);
    }

    /// <summary>
    /// Map the serialized document into the domain recipe.
    ///
    /// I enforce required sections and basic structure here.
    /// Anything semantic (valid PID, known technique name, etc) is the validator's job.
    /// </summary>
    private static RunewireRecipe MapToDomain(RecipeDocument doc)
    {
        if (doc.Target is null)
        {
            throw new RecipeLoadException("Recipe 'target' section is required.");
        }

        if (doc.Technique is null)
        {
            throw new RecipeLoadException("Recipe 'technique' section is required.");
        }

        if (doc.Payload is null)
        {
            throw new RecipeLoadException("Recipe 'payload' section is required.");
        }

        string name = doc.Name ?? string.Empty;
        string? description = doc.Description;

        string techniqueName = doc.Technique.Name ?? string.Empty;
        InjectionTechnique technique = new(techniqueName);

        RecipeTargetKind targetKind = ParseTargetKind(doc.Target.Kind);
        RecipeTarget target = targetKind switch
        {
            RecipeTargetKind.Self => RecipeTarget.Self(),
            RecipeTargetKind.ProcessById => RecipeTarget.ForProcessId(doc.Target.ProcessId ?? 0),
            RecipeTargetKind.ProcessByName => RecipeTarget.ForProcessName(doc.Target.ProcessName ?? string.Empty),
            _ => throw new RecipeLoadException($"Unsupported target kind '{doc.Target.Kind}'."),
        };

        string payloadPath = doc.Payload.Path ?? string.Empty;

        RecipeSafetyDocument safety = doc.Safety ?? new RecipeSafetyDocument();

        return new RunewireRecipe(name, description, target, technique, payloadPath, safety.RequireInteractiveConsent, safety.AllowKernelDrivers);
    }

    /// <summary>
    /// Turns the raw target kind string into a RecipeTargetKind.
    /// Throw RecipeLoadException if it is missing or unknown.
    /// I accept a few aliases to make authoring recipes less annoying.
    /// </summary>
    private static RecipeTargetKind ParseTargetKind(string? rawKind)
    {
        if (string.IsNullOrWhiteSpace(rawKind))
        {
            throw new RecipeLoadException("Recipe target 'kind' is required.");
        }

        string normalized = rawKind.Trim().ToLowerInvariant();
        return normalized switch
        {
            "self" => RecipeTargetKind.Self,

            "processbyid" => RecipeTargetKind.ProcessById,
            "process_id" => RecipeTargetKind.ProcessById,
            "processid" => RecipeTargetKind.ProcessById,
            "pid" => RecipeTargetKind.ProcessById,

            "processbyname" => RecipeTargetKind.ProcessByName,
            "process_name" => RecipeTargetKind.ProcessByName,
            "processname" => RecipeTargetKind.ProcessByName,
            "image" => RecipeTargetKind.ProcessByName,

            _ => throw new RecipeLoadException($"Unknown recipe target kind '{rawKind}'."),
        };
    }
}
