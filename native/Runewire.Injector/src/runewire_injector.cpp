#define RUNWIRE_INJECTOR_EXPORTS
#include "runewire_injector.h"
#include "technique_dispatch.h"

#include <chrono>
#include <windows.h>

namespace {
    unsigned long long now_utc_ms() {
        using namespace std::chrono;

        const auto now = std::chrono::system_clock::now();
        const auto ms = std::chrono::duration_cast<milliseconds>(now.time_since_epoch());
        return static_cast<unsigned long long>(ms.count());
    }

    void debug_log(const char* message) {
        if (!message) {
            return;
        }

        ::OutputDebugStringA("[Runewire.Injector] ");
        ::OutputDebugStringA(message);
        ::OutputDebugStringA("\n");
    }

    bool validate_request(const rw_injection_request* req, const char** error_code,
        const char** error_message) {
        if (!req) {
            if (error_code) {
                *error_code = "NULL_REQUEST";
            }
            if (error_message) {
                *error_message = "Injection request pointer was null.";
            }
            return false;
        }

        if (!req->recipe_name || req->recipe_name[0] == '\0') {
            if (error_code) {
                *error_code = "RECIPE_NAME_REQUIRED";
            }
            if (error_message) {
                *error_message = "Recipe name must be provided.";
            }
            return false;
        }

        if (!req->technique_name || req->technique_name[0] == '\0') {
            if (error_code) {
                *error_code = "TECHNIQUE_NAME_REQUIRED";
            }
            if (error_message) {
                *error_message = "Technique name must be provided.";
            }
            return false;
        }

        if (!req->payload_path || req->payload_path[0] == '\0') {
            if (error_code) {
                *error_code = "PAYLOAD_PATH_REQUIRED";
            }
            if (error_message) {
                *error_message = "Payload path must be provided.";
            }
            return false;
        }

        // technique_parameters_json is optional; no validation here.

        switch (req->target.kind) {
        case RW_TARGET_SELF:
            // No extra validation.
            break;

        case RW_TARGET_PROCESS_ID:
            if (req->target.pid == 0UL) {
                if (error_code) {
                    *error_code = "TARGET_PID_INVALID";
                }
                if (error_message) {
                    *error_message = "Target PID must be non-zero.";
                }
                return false;
            }
            break;

        case RW_TARGET_PROCESS_NAME:
            if (!req->target.process_name || req->target.process_name[0] == '\0') {
                if (error_code) {
                    *error_code = "TARGET_NAME_REQUIRED";
                }
                if (error_message) {
                    *error_message = "Target process name must be provided.";
                }
                return false;
            }
            break;

        case RW_TARGET_LAUNCH_PROCESS:
            if (!req->target.launch_path || req->target.launch_path[0] == '\0') {
                if (error_code) {
                    *error_code = "TARGET_LAUNCH_PATH_REQUIRED";
                }
                if (error_message) {
                    *error_message = "Launch path must be provided.";
                }
                return false;
            }
            break;

        default:
            if (error_code) {
                *error_code = "TARGET_KIND_UNSUPPORTED";
            }
            if (error_message) {
                *error_message = "Unsupported target kind.";
            }
            return false;
        }

        return true;
    }

} // namespace

extern "C" RW_API int rw_inject(const rw_injection_request* request,
    rw_injection_result* result) {
    // Defensive: result must be non-null so we can write an outcome.
    if (!result) {
        return -1; // API misuse.
    }

    const unsigned long long started = now_utc_ms();

    const char* error_code = nullptr;
    const char* error_message = nullptr;

    if (!validate_request(request, &error_code, &error_message)) {
        const unsigned long long completed = now_utc_ms();

        result->success = 0;
        result->error_code = error_code;
        result->error_message = error_message;
        result->started_at_utc_ms = started;
        result->completed_at_utc_ms = completed;

        debug_log("rw_inject: request validation failed (no real injection performed).");
        return 1; // Validation failure.
    }

    const dispatch_outcome outcome = dispatch_technique(request);
    if (!outcome.success) {
        const unsigned long long completed = now_utc_ms();

        result->success = 0;
        result->error_code = outcome.error_code;
        result->error_message = outcome.error_message;
        result->started_at_utc_ms = started;
        result->completed_at_utc_ms = completed;

        debug_log("rw_inject: technique unsupported or parameters invalid.");
        return 1;
    }

    debug_log("rw_inject: stub implementation invoked. No actual injection is " "performed in this build.");

    const unsigned long long completed = now_utc_ms();

    result->success = 1;
    result->error_code = nullptr;
    result->error_message = nullptr;
    result->started_at_utc_ms = started;
    result->completed_at_utc_ms = completed;

    return 0; // Success.
}
