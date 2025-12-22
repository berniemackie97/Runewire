#include "ntdll_utils.h"

#ifdef _WIN32

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

#endif
