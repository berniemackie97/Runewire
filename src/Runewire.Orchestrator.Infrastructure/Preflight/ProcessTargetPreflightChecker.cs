using System.Diagnostics;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Checks whether the recipe target can be resolved on the current host.
/// </summary>
public sealed class ProcessTargetPreflightChecker : ITargetPreflightChecker
{
    public TargetPreflightResult Check(RunewireRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        try
        {
            return recipe.Target.Kind switch
            {
                RecipeTargetKind.Self => TargetPreflightResult.Ok(),
                RecipeTargetKind.ProcessById => CheckProcessById(recipe.Target.ProcessId),
                RecipeTargetKind.ProcessByName => CheckProcessByName(recipe.Target.ProcessName),
                RecipeTargetKind.LaunchProcess => CheckLaunchTarget(recipe.Target),
                _ => TargetPreflightResult.Failed(new RecipeValidationError("TARGET_KIND_UNKNOWN", $"Unknown target kind '{recipe.Target.Kind}'.")),
            };
        }
        catch (Exception ex)
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_PRECHECK_FAILED", $"Preflight failed: {ex.Message}"));
        }
    }

    private static TargetPreflightResult CheckLaunchTarget(RecipeTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.LaunchPath))
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_LAUNCH_PATH_REQUIRED", "Launch path is required."));
        }

        if (!File.Exists(target.LaunchPath))
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_LAUNCH_PATH_NOT_FOUND", $"Launch path not found: {target.LaunchPath}"));
        }

        if (!string.IsNullOrWhiteSpace(target.LaunchWorkingDirectory) && !Directory.Exists(target.LaunchWorkingDirectory))
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_LAUNCH_WORKDIR_NOT_FOUND", $"Launch working directory not found: {target.LaunchWorkingDirectory}"));
        }

        if (target.LaunchStartSuspended && !OperatingSystem.IsWindows())
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_LAUNCH_SUSPEND_UNSUPPORTED", "Start suspended is only supported on Windows."));
        }

        return TargetPreflightResult.Ok();
    }

    private static TargetPreflightResult CheckProcessById(int? processId)
    {
        if (processId is null || processId <= 0)
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_PID_INVALID", "Process ID must be positive."));
        }

        try
        {
            _ = Process.GetProcessById(processId.Value);
            return TargetPreflightResult.Ok();
        }
        catch (ArgumentException)
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_PID_NOT_FOUND", $"Process with ID {processId} was not found."));
        }
    }

    private static TargetPreflightResult CheckProcessByName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_NAME_REQUIRED", "Process name is required when targeting by name."));
        }

        string nameWithoutExtension = rawName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? rawName[..^4]
            : rawName;

        Process[] matches = Process.GetProcessesByName(nameWithoutExtension);
        if (matches.Length == 0)
        {
            return TargetPreflightResult.Failed(new RecipeValidationError("TARGET_NAME_NOT_FOUND", $"No running process found with name '{rawName}'."));
        }

        return TargetPreflightResult.Ok();
    }
}
