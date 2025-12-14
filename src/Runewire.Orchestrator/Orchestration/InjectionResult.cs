namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Result of an injection run.
/// Success flag + optional error info + timestamps.
/// </summary>
public sealed record InjectionResult(bool Success, string? ErrorCode, string? ErrorMessage, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc)
{
    /// <summary>
    /// True when Success is false.
    /// </summary>
    public bool IsFailure => !Success;

    /// <summary>
    /// Success result with timestamps.
    /// </summary>
    public static InjectionResult Succeeded(DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc) => new(true, null, null, startedAtUtc, completedAtUtc);

    /// <summary>
    /// Failure result with error info + timestamps.
    /// </summary>
    public static InjectionResult Failed(string? errorCode, string? errorMessage, DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc) =>
        new(false, errorCode, errorMessage, startedAtUtc, completedAtUtc);
}
