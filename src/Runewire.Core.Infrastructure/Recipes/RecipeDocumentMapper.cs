using Runewire.Domain.Recipes;

namespace Runewire.Core.Infrastructure.Recipes;

/// <summary>
/// Maps serialized recipe documents into domain recipes.
/// Shared by YAML and JSON loaders so mapping stays consistent.
/// </summary>
internal static class RecipeDocumentMapper
{
    public static RunewireRecipe MapToDomain(RecipeDocument doc, string? baseDirectory = null)
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
            RecipeTargetKind.LaunchProcess => RecipeTarget.ForLaunchProcess(doc.Target.Path ?? string.Empty, doc.Target.Arguments, doc.Target.WorkingDirectory, doc.Target.StartSuspended ?? false),
            _ => throw new RecipeLoadException($"Unsupported target kind '{doc.Target.Kind}'."), // should be unreachable due to ParseTargetKind
        };

        string payloadPath = NormalizePath(doc.Payload.Path, baseDirectory);

        RecipeSafetyDocument safety = doc.Safety ?? new RecipeSafetyDocument();

        IReadOnlyList<RecipeStep>? steps = MapSteps(doc.Steps, baseDirectory);

        return new RunewireRecipe(name, description, target, technique, payloadPath, safety.RequireInteractiveConsent, safety.AllowKernelDrivers, steps);
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

            "launchprocess" => RecipeTargetKind.LaunchProcess,
            "launch" => RecipeTargetKind.LaunchProcess,

            _ => throw new RecipeLoadException($"Unknown recipe target kind '{rawKind}'."), // explicit to keep authoring feedback clear
        };
    }

    private static IReadOnlyList<RecipeStep>? MapSteps(List<RecipeStepDocument>? stepsDoc, string? baseDirectory)
    {
        if (stepsDoc is null || stepsDoc.Count == 0)
        {
            return null;
        }

        List<RecipeStep> steps = new(stepsDoc.Count);
        foreach (RecipeStepDocument doc in stepsDoc)
        {
            RecipeStepKind kind = ParseStepKind(doc.Kind);
            RecipeStep step = new(kind)
            {
                TechniqueName = doc.TechniqueName,
                TechniqueParameters = doc.TechniqueParameters,
                PayloadPath = NormalizePath(doc.PayloadPath, baseDirectory),
                WaitMilliseconds = doc.WaitMilliseconds,
                Condition = MapCondition(doc.Condition)
            };
            steps.Add(step);
        }

        return steps;
    }

    private static RecipeStepKind ParseStepKind(string? rawKind)
    {
        if (string.IsNullOrWhiteSpace(rawKind))
        {
            throw new RecipeLoadException("Step 'kind' is required.");
        }

        string normalized = rawKind.Trim().ToLowerInvariant();
        return normalized switch
        {
            "inject" => RecipeStepKind.InjectTechnique,
            "injecttechnique" => RecipeStepKind.InjectTechnique,
            "wait" => RecipeStepKind.Wait,
            "suspend" => RecipeStepKind.Suspend,
            "resume" => RecipeStepKind.Resume,
            _ => throw new RecipeLoadException($"Unknown step kind '{rawKind}'."),
        };
    }

    private static WaitCondition? MapCondition(WaitConditionDocument? conditionDoc)
    {
        if (conditionDoc is null)
        {
            return null;
        }

        WaitConditionKind kind = ParseConditionKind(conditionDoc.Kind);
        string value = conditionDoc.Value ?? string.Empty;
        return new WaitCondition(kind, value, conditionDoc.TimeoutMilliseconds);
    }

    private static WaitConditionKind ParseConditionKind(string? rawKind)
    {
        if (string.IsNullOrWhiteSpace(rawKind))
        {
            throw new RecipeLoadException("Wait condition 'kind' is required.");
        }

        string normalized = rawKind.Trim().ToLowerInvariant();
        return normalized switch
        {
            "moduleloaded" => WaitConditionKind.ModuleLoaded,
            "module" => WaitConditionKind.ModuleLoaded,
            "dll" => WaitConditionKind.ModuleLoaded,
            "fileexists" => WaitConditionKind.FileExists,
            "file" => WaitConditionKind.FileExists,
            "processexited" => WaitConditionKind.ProcessExited,
            "processexit" => WaitConditionKind.ProcessExited,
            "windowclass" => WaitConditionKind.WindowClass,
            "windowtitle" => WaitConditionKind.WindowTitle,
            "namedpipe" => WaitConditionKind.NamedPipeAvailable,
            "pipe" => WaitConditionKind.NamedPipeAvailable,
            "processhandleready" => WaitConditionKind.ProcessHandleReady,
            "handle" => WaitConditionKind.ProcessHandleReady,
            "tcport" => WaitConditionKind.TcpPortListening,
            "tcplistening" => WaitConditionKind.TcpPortListening,
            "tcpportlistening" => WaitConditionKind.TcpPortListening,
            "event" => WaitConditionKind.NamedEvent,
            "mutex" => WaitConditionKind.NamedMutex,
            "semaphore" => WaitConditionKind.NamedSemaphore,
            "sharedmemory" => WaitConditionKind.SharedMemoryExists,
            "shm" => WaitConditionKind.SharedMemoryExists,
            "registry" => WaitConditionKind.RegistryValueEquals,
            "childprocess" => WaitConditionKind.ChildProcessAppeared,
            "child" => WaitConditionKind.ChildProcessAppeared,
            "http" => WaitConditionKind.HttpReachable,
            "httpreachable" => WaitConditionKind.HttpReachable,
            "service" => WaitConditionKind.ServiceState,
            "env" => WaitConditionKind.EnvironmentVariableEquals,
            "environment" => WaitConditionKind.EnvironmentVariableEquals,
            "filecontent" => WaitConditionKind.FileContentContains,
            "shmvalue" => WaitConditionKind.SharedMemoryValueEquals,
            _ => throw new RecipeLoadException($"Unknown wait condition kind '{rawKind}'."),
        };
    }

    private static string NormalizePath(string? path, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }
}
