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
            _ => $"unknown target kind {request.Target.Kind}",
        };
    }
}
