namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// High level payload format kinds used during preflight.
/// </summary>
internal enum PayloadKind
{
    Unknown = 0,
    PE = 1,
    ELF = 2,
    MachO = 3,
}
