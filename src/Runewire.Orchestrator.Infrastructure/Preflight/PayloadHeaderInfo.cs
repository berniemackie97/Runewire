namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Light header info extracted from a payload to support preflight checks.
/// </summary>
internal readonly record struct PayloadHeaderInfo(PayloadKind Kind, string? Architecture);
