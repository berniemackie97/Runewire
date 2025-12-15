#include "handler_threads.h"
#include "process_utils.h"
#include "thread_utils.h"
#include "payload_utils.h"

#include <cstring>
#include <string>
dispatch_outcome handle_create_remote_thread(const rw_injection_request* req, const parsed_params&)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    const char* payload_path = req ? req->payload_path : nullptr;
    if (!payload_exists(payload_path))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "DLL payload was not found." };
    }

    // Resolve LoadLibraryA.
    HMODULE kernel32 = ::GetModuleHandleA("kernel32.dll");
    if (!kernel32)
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PROC_ADDRESS_NOT_FOUND", "Could not resolve kernel32." };
    }

    FARPROC load_library = ::GetProcAddress(kernel32, "LoadLibraryA");
    if (!load_library)
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PROC_ADDRESS_NOT_FOUND", "Could not resolve LoadLibraryA." };
    }

    const size_t path_len = std::strlen(payload_path) + 1;
    LPVOID remote_buffer = nullptr;
    if (process == ::GetCurrentProcess())
    {
        remote_buffer = ::VirtualAlloc(nullptr, path_len, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    }
    else
    {
        remote_buffer = ::VirtualAllocEx(process, nullptr, path_len, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    }

    if (!remote_buffer)
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for DLL path." };
    }

    BOOL write_ok = FALSE;
    if (process == ::GetCurrentProcess())
    {
        std::memcpy(remote_buffer, payload_path, path_len);
        write_ok = TRUE;
    }
    else
    {
        write_ok = ::WriteProcessMemory(process, remote_buffer, payload_path, path_len, nullptr);
    }

    if (!write_ok)
    {
        if (process == ::GetCurrentProcess())
        {
            ::VirtualFree(remote_buffer, 0, MEM_RELEASE);
        }
        else
        {
            ::VirtualFreeEx(process, remote_buffer, 0, MEM_RELEASE);
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_WRITE_FAILED", "Failed to write DLL path to target process." };
    }

    HANDLE thread = nullptr;
    if (process == ::GetCurrentProcess())
    {
        thread = ::CreateThread(nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(load_library), remote_buffer, 0, nullptr);
    }
    else
    {
        thread = ::CreateRemoteThread(process, nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(load_library), remote_buffer, 0, nullptr);
    }

    if (!thread)
    {
        if (process == ::GetCurrentProcess())
        {
            ::VirtualFree(remote_buffer, 0, MEM_RELEASE);
        }
        else
        {
            ::VirtualFreeEx(process, remote_buffer, 0, MEM_RELEASE);
            ::CloseHandle(process);
        }
        return { false, "THREAD_CREATE_FAILED", "Failed to start LoadLibraryA thread." };
    }

    ::WaitForSingleObject(thread, INFINITE);
    DWORD exit_code = 0;
    ::GetExitCodeThread(thread, &exit_code);
    ::CloseHandle(thread);

    if (process == ::GetCurrentProcess())
    {
        ::VirtualFree(remote_buffer, 0, MEM_RELEASE);
    }
    else
    {
        ::VirtualFreeEx(process, remote_buffer, 0, MEM_RELEASE);
        ::CloseHandle(process);
    }

    if (exit_code == 0)
    {
        return { false, "DLL_LOAD_FAILED", "LoadLibraryA failed in target process." };
    }

    return { true, nullptr, nullptr };
}

dispatch_outcome handle_queue_user_apc(const rw_injection_request* req, const parsed_params& params)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    // Optional threadId must be positive if provided.
    if (const auto thread_id = params.get_int("threadId"))
    {
        HANDLE thread = open_thread_for_injection(static_cast<DWORD>(*thread_id), failure);
        if (!thread)
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return failure;
        }
        ::CloseHandle(thread);
    }

    // Optional timeoutMs must be non-negative if provided.
    if (const auto timeout = params.get_int("timeoutMs"))
    {
        if (*timeout < 0)
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "TECHNIQUE_PARAM_INVALID", "timeoutMs must be zero or greater." };
        }
    }

    if (process != ::GetCurrentProcess())
    {
        ::CloseHandle(process);
    }
    return { true, nullptr, nullptr }; // Stub: reachability only.
}

dispatch_outcome handle_nt_create_thread_ex(const rw_injection_request* req, const parsed_params&)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    if (process != ::GetCurrentProcess())
    {
        ::CloseHandle(process);
    }
    return { true, nullptr, nullptr }; // Stub: reachability only.
}

dispatch_outcome handle_thread_hijack(const rw_injection_request* req, const parsed_params& params)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    // Optional threadId parameter must be positive if present.
    if (const auto thread_id = params.get_int("threadId"))
    {
        HANDLE thread = open_thread_for_injection(static_cast<DWORD>(*thread_id), failure);
        if (!thread)
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return failure;
        }
        ::CloseHandle(thread);
    }

    if (process != ::GetCurrentProcess())
    {
        ::CloseHandle(process);
    }
    return { true, nullptr, nullptr }; // Stub: reachability only.
}
