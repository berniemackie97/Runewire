#pragma once

#include <windows.h>
#include "technique_dispatch.h"

// Opens a thread handle with common injection rights. Returns nullptr and sets failure on error.
HANDLE open_thread_for_injection(DWORD thread_id, dispatch_outcome& failure);
