#pragma once

#include "technique_dispatch.h"
#ifdef _WIN32
#include <windows.h>
#else
using HANDLE = void*;
using DWORD = unsigned long;
#endif

// Opens a thread handle with common injection rights. Returns nullptr and sets failure on error.
HANDLE open_thread_for_injection(DWORD thread_id, dispatch_outcome& failure);
