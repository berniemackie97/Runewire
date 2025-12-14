namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Result of waiting for a target condition.
/// </summary>
public sealed record WaitResult(bool Success, string? ErrorCode, string? ErrorMessage)
{
    public static WaitResult Succeeded() => new(true, null, null);

    public static WaitResult Failed(string code, string message) => new(false, code, message);
}
