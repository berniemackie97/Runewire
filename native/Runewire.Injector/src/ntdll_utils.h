#pragma once

#ifdef _WIN32
#include <windows.h>

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

NtMapViewOfSection_t resolve_nt_map_view_of_section();
NtUnmapViewOfSection_t resolve_nt_unmap_view_of_section();
#else
using NtMapViewOfSection_t = void*;
using NtUnmapViewOfSection_t = void*;

inline NtMapViewOfSection_t resolve_nt_map_view_of_section() { return nullptr; }
inline NtUnmapViewOfSection_t resolve_nt_unmap_view_of_section() { return nullptr; }
#endif
