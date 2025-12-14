namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Result of a target control operation like suspend or resume.
/// </summary>
public sealed record TargetControlResult(bool Success, string? ErrorCode, string? ErrorMessage)
{
    public static TargetControlResult Succeeded() => new(true, null, null);

    public static TargetControlResult Failed(string errorCode, string errorMessage) => new(false, errorCode, errorMessage);
}
