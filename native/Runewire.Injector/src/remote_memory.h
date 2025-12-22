#pragma once

#include <cstddef>

#ifdef _WIN32
#include <windows.h>
#else
using HANDLE = void*;
using DWORD = unsigned long;
#endif

void* alloc_target_memory(HANDLE process, size_t size, DWORD protect, bool is_self);
void free_target_memory(HANDLE process, void* address, bool is_self);
bool write_target_memory(HANDLE process, void* destination, const void* source, size_t size, bool is_self);
