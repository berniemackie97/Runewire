#pragma once

#include "../include/runewire_injector.h"

struct dispatch_outcome
{
    bool success;
    const char* error_code;
    const char* error_message;
};

// Validates and dispatches a technique. Returned pointers refer to static strings.
dispatch_outcome dispatch_technique(const rw_injection_request* req);
