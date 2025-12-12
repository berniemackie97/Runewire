using Runewire.Core.Domain.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Cli.Infrastructure;

/// <summary>
/// Injection engine that performs a "dry run" only, emitting a human-readable
/// description of the planned injection instead of interacting with processes.
///
/// This is useful for:
/// - Early CLI wiring and demos.
/// - Safe default behavior in labs where actual injection is disabled.
/// </summary>
/// <remarks>
/// Creates a new <see cref="DryRunInjectionEngine"/>.
/// </remarks>
/// <param name="output">
/// Destination for human-readable output. When <c>null</c>, defaults to <see cref="Console.Out"/>.
/// Supplying a <see cref="TextWriter"/> makes the engine easy to unit test or redirect in other hosts.
/// </param>
public sealed class DryRunInjectionEngine(TextWriter? output = null) : IInjectionEngine
{
    private readonly TextWriter _output = output ?? Console.Out;

    /// <summary>
    /// Executes the dry-run "injection": no processes are touched. Instead, a textual
    /// description of what would happen is written to the configured output.
    /// </summary>
    /// <param name="request">The injection request describing target, technique, and payload.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A completed <see cref="InjectionResult"/> representing a successful, instantaneous run.
    /// </returns>
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

        // We pretend the injection both started and completed at the same time.
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
