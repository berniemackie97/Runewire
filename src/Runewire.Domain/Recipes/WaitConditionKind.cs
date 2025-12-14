namespace Runewire.Domain.Recipes;

/// <summary>
/// Types of wait conditions supported by workflow steps.
/// </summary>
public enum WaitConditionKind
{
    ModuleLoaded = 0,
    FileExists = 1,
    ProcessExited = 2,
    WindowClass = 3,
    WindowTitle = 4,
    NamedPipeAvailable = 5,
    ProcessHandleReady = 6,
    TcpPortListening = 7,
    NamedEvent = 8,
    NamedMutex = 9,
    NamedSemaphore = 10,
    SharedMemoryExists = 11,
    RegistryValueEquals = 12,
    ChildProcessAppeared = 13,
    HttpReachable = 14,
    ServiceState = 15,
    EnvironmentVariableEquals = 16,
    FileContentContains = 17,
}
