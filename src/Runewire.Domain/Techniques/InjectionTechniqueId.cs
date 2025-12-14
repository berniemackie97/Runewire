namespace Runewire.Domain.Techniques;

/// <summary>
/// Stable ids for built in techniques.
/// </summary>
public enum InjectionTechniqueId
{
    /// <summary>
    /// Unknown or custom technique id.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// DLL injection via CreateRemoteThread.
    /// </summary>
    CreateRemoteThread = 1,

    // QueueUserAPC = 2,
    // NtCreateThreadEx = 3,
    // KernelDriverLoad = 10,
}
