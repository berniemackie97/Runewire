#include "process_utils.h"
#include "technique_dispatch.h"

#ifdef _WIN32

#include <cctype>
#include <string>
#include <tlhelp32.h>

namespace
{
    std::string normalize_process_name(const char* name)
    {
        std::string value = name ? name : "";
        for (char& ch : value)
        {
            ch = static_cast<char>(std::tolower(static_cast<unsigned char>(ch)));
        }

        const char* exe_suffix = ".exe";
        if (value.size() > 4 && value.compare(value.size() - 4, 4, exe_suffix) == 0)
        {
            value.resize(value.size() - 4);
        }

        return value;
    }

    std::string entry_process_name(const PROCESSENTRY32& entry)
    {
#ifdef UNICODE
        const wchar_t* wide = entry.szExeFile;
        if (!wide || wide[0] == L'\0')
        {
            return {};
        }

        const int needed = ::WideCharToMultiByte(CP_UTF8, 0, wide, -1, nullptr, 0, nullptr, nullptr);
        if (needed <= 1)
        {
            return {};
        }

        std::string result(static_cast<size_t>(needed - 1), '\0');
        ::WideCharToMultiByte(CP_UTF8, 0, wide, -1, result.data(), needed, nullptr, nullptr);
        return result;
#else
        return std::string(entry.szExeFile);
#endif
    }

    bool try_find_process_id_by_name(const char* name, DWORD& pid)
    {
        if (!name || name[0] == '\0')
        {
            return false;
        }

        std::string desired = normalize_process_name(name);
        if (desired.empty())
        {
            return false;
        }

        HANDLE snapshot = ::CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        PROCESSENTRY32 entry{};
        entry.dwSize = sizeof(entry);

        if (!::Process32First(snapshot, &entry))
        {
            ::CloseHandle(snapshot);
            return false;
        }

        do
        {
            if (normalize_process_name(entry_process_name(entry).c_str()) == desired)
            {
                pid = entry.th32ProcessID;
                ::CloseHandle(snapshot);
                return true;
            }
        } while (::Process32Next(snapshot, &entry));

        ::CloseHandle(snapshot);
        return false;
    }
}

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

    if (req->target.kind == RW_TARGET_PROCESS_NAME)
    {
        DWORD pid = 0;
        if (!try_find_process_id_by_name(req->target.process_name, pid) || pid == 0)
        {
            failure = { false, "TARGET_NAME_NOT_FOUND", "Target process name was not found." };
            return nullptr;
        }

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

    failure = { false, "TARGET_KIND_UNSUPPORTED", "Technique supports only self, process id, or process name targets." };
    return nullptr;
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

#else

HANDLE open_process_for_injection(const rw_injection_request*, DWORD, dispatch_outcome& failure)
{
    failure = { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Native injector is not implemented on this platform." };
    return nullptr;
}

bool get_is_wow64(HANDLE, bool& is_wow64)
{
    is_wow64 = false;
    return false;
}

#endif
