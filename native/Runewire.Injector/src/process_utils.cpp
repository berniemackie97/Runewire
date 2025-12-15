#include "process_utils.h"
#include "technique_dispatch.h"

HANDLE open_process_for_injection(const rw_injection_request* req, DWORD desired_access, dispatch_outcome& failure)
{
    if (!req)
    {
        failure = { false, "NULL_REQUEST", "Injection request pointer was null." };
        return nullptr;
    }

    if (req->target.kind == RW_TARGET_SELF)
    {
        return ::GetCurrentProcess();
    }

    if (req->target.kind == RW_TARGET_PROCESS_ID)
    {
        const DWORD pid = static_cast<DWORD>(req->target.pid);
        // Treat current PID as self to avoid permission issues with OpenProcess.
        if (pid == ::GetCurrentProcessId())
        {
            return ::GetCurrentProcess();
        }

        HANDLE process = ::OpenProcess(desired_access, FALSE, pid);
        if (!process)
        {
            failure = { false, "TARGET_OPEN_FAILED", "Failed to open target process." };
            return nullptr;
        }

        return process;
    }

    if (req->target.kind != RW_TARGET_PROCESS_ID)
    {
        failure = { false, "TARGET_KIND_UNSUPPORTED", "Technique supports only self or process id targets." };
        return nullptr;
    }
    return nullptr;
}
