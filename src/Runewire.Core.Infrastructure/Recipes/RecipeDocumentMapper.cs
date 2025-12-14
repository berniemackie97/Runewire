using Runewire.Domain.Recipes;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Maps serialized recipe documents into domain recipes.
/// Shared by YAML and JSON loaders so mapping stays consistent.
/// </summary>
internal static class RecipeDocumentMapper
{
    public static RunewireRecipe MapToDomain(RecipeDocument doc)
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
        IReadOnlyDictionary<string, string>? techniqueParameters = doc.Technique.Parameters;
        InjectionTechnique technique = new(techniqueName, techniqueParameters);

        RecipeTargetKind targetKind = ParseTargetKind(doc.Target.Kind);
        RecipeTarget target = targetKind switch
        {
            RecipeTargetKind.Self => RecipeTarget.Self(),
            RecipeTargetKind.ProcessById => RecipeTarget.ForProcessId(doc.Target.ProcessId ?? 0),
            RecipeTargetKind.ProcessByName => RecipeTarget.ForProcessName(doc.Target.ProcessName ?? string.Empty),
            _ => throw new RecipeLoadException($"Unsupported target kind '{doc.Target.Kind}'."), // should be unreachable due to ParseTargetKind
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

            _ => throw new RecipeLoadException($"Unknown recipe target kind '{rawKind}'."), // explicit to keep authoring feedback clear
        };
    }
}
