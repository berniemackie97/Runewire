#pragma once

// Native injection engine entrypoints for Runewire.Injector

#ifdef _WIN32
#ifdef RUNWIRE_INJECTOR_EXPORTS
#define RW_API __declspec(dllexport)
#else
#define RW_API __declspec(dllimport)
#endif
#else
#define RW_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

    /// Target kind for injection.
    typedef enum rw_target_kind
    {
        RW_TARGET_SELF = 0,
        RW_TARGET_PROCESS_ID = 1,
        RW_TARGET_PROCESS_NAME = 2,
        RW_TARGET_LAUNCH_PROCESS = 3
    } rw_target_kind;

    /// Target description.
    typedef struct rw_target
    {
        /// Kind of target.
        rw_target_kind kind;

        /// Process ID. Only meaningful when kind == RW_TARGET_PROCESS_ID.
        unsigned long pid;

        /// Process name. Only meaningful when kind == RW_TARGET_PROCESS_NAME.
        /// UTF-8, owned by the caller, null-terminated.
        const char* process_name;

        /// Launch path. Only meaningful when kind == RW_TARGET_LAUNCH_PROCESS.
        const char* launch_path;

        /// Launch arguments. Only meaningful when kind == RW_TARGET_LAUNCH_PROCESS.
        const char* launch_arguments;

        /// Launch working directory. Only meaningful when kind == RW_TARGET_LAUNCH_PROCESS.
        const char* launch_working_directory;

        /// Non-zero to start suspended. Only meaningful when kind == RW_TARGET_LAUNCH_PROCESS.
        int launch_start_suspended;
    } rw_target;

    /// Injection request description. All strings are UTF-8 and owned by the caller.
    /// The callee must not attempt to free them.
    typedef struct rw_injection_request
    {
        /// Human-readable recipe name. Required.
        const char* recipe_name;

        /// Optional recipe description. May be NULL.
        const char* recipe_description;

        /// Target description.
        rw_target target;

        /// Logical injection technique identifier, e.g. "CreateRemoteThread".
        /// Required.
        const char* technique_name;

        /// Optional JSON blob of technique parameters (UTF-8). May be NULL.
        const char* technique_parameters_json;

        /// Fully-qualified path to the payload to inject. Required.
        const char* payload_path;

        /// 0 = false, non-zero = true.
        int allow_kernel_drivers;

        /// 0 = false, non-zero = true.
        int require_interactive_consent;
    } rw_injection_request;

    /// Injection result. All strings are UTF-8.
    /// For now, error_code and error_message will either be NULL or point
    /// to internal static strings that do not need to be freed by the caller.
    typedef struct rw_injection_result
    {
        /// 0 = failure, non-zero = success.
        int success;

        /// Optional error code for machine-readable failure classification.
        const char* error_code;

        /// Optional error message for diagnostics.
        const char* error_message;

        /// Unix epoch milliseconds, UTC.
        unsigned long long started_at_utc_ms;
        unsigned long long completed_at_utc_ms;
    } rw_injection_result;

    /// Perform an injection.
    ///
    /// At this stage, the implementation performs basic validation and returns
    /// success without touching any processes. Future versions will preserve
    /// this ABI while expanding supported behavior.
    ///
    /// Returns:
    ///   0 on success (request accepted and result populated),
    ///   non-zero on API-level failure (e.g., invalid arguments / validation).
    RW_API int rw_inject(const rw_injection_request* request, rw_injection_result* result);

#ifdef __cplusplus
}
#endif
