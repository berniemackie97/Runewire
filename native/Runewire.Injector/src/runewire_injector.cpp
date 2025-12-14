#define RUNWIRE_INJECTOR_EXPORTS
#include "runewire_injector.h"

#include <chrono>
#include <cctype>
#include <cstring>
#include <string>
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

    bool equals_ignore_case(const char* a, const char* b) {
        if (!a || !b) {
            return false;
        }

        while (*a && *b) {
            if (std::tolower(static_cast<unsigned char>(*a)) != std::tolower(static_cast<unsigned char>(*b))) {
                return false;
            }
            ++a;
            ++b;
        }

        return *a == '\0' && *b == '\0';
    }

    bool json_has_key(const char* json, const char* key) {
        if (!json || !key) {
            return false;
        }

        const size_t key_len = std::strlen(key);
        const std::string pattern = "\"" + std::string(key) + "\"";
        return std::string(json).find(pattern) != std::string::npos;
    }

    bool validate_params_object(const char* json, const char** error_code, const char** error_message) {
        if (json && json[0] != '\0' && json[0] != '{') {
            if (error_code) {
                *error_code = "TECHNIQUE_PARAMS_INVALID";
            }
            if (error_message) {
                *error_message = "Technique parameters must be a JSON object.";
            }
            return false;
        }
        return true;
    }

    bool dispatch_technique(const rw_injection_request* req, const char** error_code, const char** error_message) {
        if (!validate_params_object(req->technique_parameters_json, error_code, error_message)) {
            return false;
        }

        if (equals_ignore_case(req->technique_name, "CreateRemoteThread") ||
            equals_ignore_case(req->technique_name, "QueueUserAPC") ||
            equals_ignore_case(req->technique_name, "NtCreateThreadEx") ||
            equals_ignore_case(req->technique_name, "ManualMap") ||
            equals_ignore_case(req->technique_name, "Shellcode") ||
            equals_ignore_case(req->technique_name, "ThreadHijack") ||
            equals_ignore_case(req->technique_name, "EarlyBirdApc") ||
            equals_ignore_case(req->technique_name, "ProcessHollowing") ||
            equals_ignore_case(req->technique_name, "ProcessDoppelganging") ||
            equals_ignore_case(req->technique_name, "ProcessHerpaderping") ||
            equals_ignore_case(req->technique_name, "ModuleStomping") ||
            equals_ignore_case(req->technique_name, "SharedSectionMap") ||
            equals_ignore_case(req->technique_name, "ReflectiveDll") ||
            equals_ignore_case(req->technique_name, "ClrHost") ||
            equals_ignore_case(req->technique_name, "PtraceInject") ||
            equals_ignore_case(req->technique_name, "MemfdShellcode") ||
            equals_ignore_case(req->technique_name, "MachThreadInject")) {
            // Stub: actual implementation to be filled in per technique.
            return true;
        }

        if (equals_ignore_case(req->technique_name, "InlineHook")) {
            if (!json_has_key(req->technique_parameters_json, "moduleName") ||
                !json_has_key(req->technique_parameters_json, "functionName")) {
                if (error_code) {
                    *error_code = "TECHNIQUE_PARAM_REQUIRED";
                }
                if (error_message) {
                    *error_message = "InlineHook requires moduleName and functionName parameters.";
                }
                return false;
            }
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "InlineHook is not implemented in this build.";
            }
            return false;
        }

        if (equals_ignore_case(req->technique_name, "IatHook")) {
            if (!json_has_key(req->technique_parameters_json, "moduleName") ||
                !json_has_key(req->technique_parameters_json, "importName")) {
                if (error_code) {
                    *error_code = "TECHNIQUE_PARAM_REQUIRED";
                }
                if (error_message) {
                    *error_message = "IatHook requires moduleName and importName parameters.";
                }
                return false;
            }
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "IatHook is not implemented in this build.";
            }
            return false;
        }

        if (equals_ignore_case(req->technique_name, "EatHook")) {
            if (!json_has_key(req->technique_parameters_json, "moduleName") ||
                !json_has_key(req->technique_parameters_json, "exportName")) {
                if (error_code) {
                    *error_code = "TECHNIQUE_PARAM_REQUIRED";
                }
                if (error_message) {
                    *error_message = "EatHook requires moduleName and exportName parameters.";
                }
                return false;
            }
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "EatHook is not implemented in this build.";
            }
            return false;
        }

        if (equals_ignore_case(req->technique_name, "WinsockRedirect")) {
            if (!json_has_key(req->technique_parameters_json, "targetHost") ||
                !json_has_key(req->technique_parameters_json, "targetPort")) {
                if (error_code) {
                    *error_code = "TECHNIQUE_PARAM_REQUIRED";
                }
                if (error_message) {
                    *error_message = "WinsockRedirect requires targetHost and targetPort parameters.";
                }
                return false;
            }
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "WinsockRedirect is not implemented in this build.";
            }
            return false;
        }

        if (equals_ignore_case(req->technique_name, "HttpRedirect")) {
            if (!json_has_key(req->technique_parameters_json, "targetUrl")) {
                if (error_code) {
                    *error_code = "TECHNIQUE_PARAM_REQUIRED";
                }
                if (error_message) {
                    *error_message = "HttpRedirect requires targetUrl parameter.";
                }
                return false;
            }
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "HttpRedirect is not implemented in this build.";
            }
            return false;
        }

        if (equals_ignore_case(req->technique_name, "DnsOverride")) {
            if (!json_has_key(req->technique_parameters_json, "host") ||
                !json_has_key(req->technique_parameters_json, "address")) {
                if (error_code) {
                    *error_code = "TECHNIQUE_PARAM_REQUIRED";
                }
                if (error_message) {
                    *error_message = "DnsOverride requires host and address parameters.";
                }
                return false;
            }
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "DnsOverride is not implemented in this build.";
            }
            return false;
        }

        if (equals_ignore_case(req->technique_name, "FileSystemRedirect")) {
            if (!json_has_key(req->technique_parameters_json, "targetPath") ||
                !json_has_key(req->technique_parameters_json, "redirectPath")) {
                if (error_code) {
                    *error_code = "TECHNIQUE_PARAM_REQUIRED";
                }
                if (error_message) {
                    *error_message = "FileSystemRedirect requires targetPath and redirectPath parameters.";
                }
                return false;
            }
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "FileSystemRedirect is not implemented in this build.";
            }
            return false;
        }

        if (equals_ignore_case(req->technique_name, "TlsBypass")) {
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "TlsBypass is not implemented in this build.";
            }
            return false;
        }

        if (equals_ignore_case(req->technique_name, "EarlyBirdCreateProcess") ||
            equals_ignore_case(req->technique_name, "EarlyBirdQueueApc") ||
            equals_ignore_case(req->technique_name, "SectionCopyExecute") ||
            equals_ignore_case(req->technique_name, "ThreadpoolApc") ||
            equals_ignore_case(req->technique_name, "ModuleStompRestore") ||
            equals_ignore_case(req->technique_name, "CallExportInit") ||
            equals_ignore_case(req->technique_name, "LdPreloadLaunch") ||
            equals_ignore_case(req->technique_name, "DyldInsertLaunch") ||
            equals_ignore_case(req->technique_name, "PtraceThreadHijack") ||
            equals_ignore_case(req->technique_name, "MemoryScanPatch") ||
            equals_ignore_case(req->technique_name, "AntiHookDetect") ||
            equals_ignore_case(req->technique_name, "SnapshotRestore")) {
            if (error_code) {
                *error_code = "TECHNIQUE_NOT_IMPLEMENTED";
            }
            if (error_message) {
                *error_message = "Technique is not implemented in this build.";
            }
            return false;
        }

        if (error_code) {
            *error_code = "TECHNIQUE_UNSUPPORTED";
        }
        if (error_message) {
            *error_message = "Technique is not implemented in this build.";
        }
        return false;
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

    if (!dispatch_technique(request, &error_code, &error_message)) {
        const unsigned long long completed = now_utc_ms();

        result->success = 0;
        result->error_code = error_code;
        result->error_message = error_message;
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
