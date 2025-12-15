#include "thread_utils.h"

#ifdef _WIN32

HANDLE open_thread_for_injection(DWORD thread_id, dispatch_outcome& failure)
{
    if (thread_id == 0)
    {
        failure = { false, "TECHNIQUE_PARAM_INVALID", "threadId must be greater than zero." };
        return nullptr;
    }

    HANDLE thread = ::OpenThread(THREAD_SET_CONTEXT | THREAD_SUSPEND_RESUME | THREAD_QUERY_INFORMATION, FALSE, thread_id);
    if (!thread)
    {
        failure = { false, "THREAD_OPEN_FAILED", "Failed to open target thread." };
        return nullptr;
    }

    return thread;
}

#else

HANDLE open_thread_for_injection(DWORD, dispatch_outcome& failure)
{
    failure = { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Native injector is not implemented on this platform." };
    return nullptr;
}

#endif
