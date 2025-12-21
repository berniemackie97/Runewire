using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.InjectionEngines;

/// <summary>
/// Dry run engine. Does not touch processes.
/// It just prints what would happen, so I can test recipes and wiring safely.
/// </summary>
public sealed class DryRunInjectionEngine(TextWriter? output = null) : IInjectionEngine
{
    // Default to Console.Out, but allow injection for tests and other hosts.
    private readonly TextWriter _output = output ?? Console.Out;

    /// <summary>
    /// No real injection. Just dump a plan and return success.
    /// </summary>
    public Task<InjectionResult> ExecuteAsync(InjectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        _output.WriteLine("Dry-run injection plan:");
        _output.WriteLine($"  Recipe:   {request.RecipeName}");

        if (!string.IsNullOrWhiteSpace(request.RecipeDescription))
        {
            _output.WriteLine($"  Summary:  {request.RecipeDescription}");
        }

        _output.WriteLine($"  Target:   {DescribeTarget(request)}");
        _output.WriteLine($"  Technique:{request.TechniqueName}");
        if (request.TechniqueParameters is { Count: > 0 })
        {
            foreach ((string key, string value) in request.TechniqueParameters)
            {
                _output.WriteLine($"    Param: {key} = {value}");
            }
        }
        _output.WriteLine($"  Payload:  {request.PayloadPath}");
        _output.WriteLine($"  Kernel:   {(request.AllowKernelDrivers ? "allowed" : "not allowed")}");
        _output.WriteLine($"  Consent:  {(request.RequireInteractiveConsent ? "required" : "not required")}");

        // Dry run completes instantly.
        return Task.FromResult(InjectionResult.Succeeded(now, now));
    }

    private static string DescribeTarget(InjectionRequest request)
    {
        return request.Target.Kind switch
        {
            RecipeTargetKind.Self => "self (current Runewire process)",
            RecipeTargetKind.ProcessById => $"PID {request.Target.ProcessId}",
            RecipeTargetKind.ProcessByName => $"process \"{request.Target.ProcessName}\"",
            RecipeTargetKind.LaunchProcess => DescribeLaunchTarget(request.Target),
            _ => $"unknown target kind {request.Target.Kind}",
        };
    }

    private static string DescribeLaunchTarget(RecipeTarget target)
    {
        string path = string.IsNullOrWhiteSpace(target.LaunchPath) ? "<missing path>" : target.LaunchPath;
        string args = string.IsNullOrWhiteSpace(target.LaunchArguments) ? string.Empty : $" {target.LaunchArguments}";
        string workingDir = string.IsNullOrWhiteSpace(target.LaunchWorkingDirectory)
            ? string.Empty
            : $" (cwd: {target.LaunchWorkingDirectory})";
        string suspended = target.LaunchStartSuspended ? " (start suspended)" : string.Empty;

        return $"launch \"{path}\"{args}{workingDir}{suspended}";
    }
}
