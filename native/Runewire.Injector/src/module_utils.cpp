#include "module_utils.h"

#include <algorithm>
#include <cctype>

#ifdef _WIN32

namespace
{
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
}

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

#else

bool find_module_entry(DWORD, const std::optional<std::string>&, MODULEENTRY32&)
{
    return false;
}

#endif
