#pragma once

#include "../include/runewire_injector.h"

#ifdef _WIN32
#include <windows.h>
#else
using HANDLE = void*;
using DWORD = unsigned long;
#endif

struct dispatch_outcome;

// Opens target process for injection operations. Returns current process handle for self-targets.
HANDLE open_process_for_injection(const rw_injection_request* req, DWORD desired_access, dispatch_outcome& failure);
