namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Result of launching a target process.
/// </summary>
public sealed record TargetLaunchResult(bool Success, int? ProcessId, string? ErrorCode, string? ErrorMessage)
{
    public static TargetLaunchResult Succeeded(int processId) => new(true, processId, null, null);

    public static TargetLaunchResult Failed(string errorCode, string errorMessage) => new(false, null, errorCode, errorMessage);
}
