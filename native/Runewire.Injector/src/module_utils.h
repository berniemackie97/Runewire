#pragma once

#include <optional>
#include <string>

#ifdef _WIN32
#include <windows.h>
#include <tlhelp32.h>
#else
using DWORD = unsigned long;
struct MODULEENTRY32 {};
#endif

bool find_module_entry(DWORD pid, const std::optional<std::string>& module_name, MODULEENTRY32& out_entry);
