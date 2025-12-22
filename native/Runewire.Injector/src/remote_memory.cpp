#include "remote_memory.h"

#include <cstring>

#ifdef _WIN32

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

#else

void* alloc_target_memory(HANDLE, size_t, DWORD, bool)
{
    return nullptr;
}

void free_target_memory(HANDLE, void*, bool)
{
}

bool write_target_memory(HANDLE, void*, const void*, size_t, bool)
{
    return false;
}

#endif
