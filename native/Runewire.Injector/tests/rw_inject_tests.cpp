#include <cassert>
#include <cstring>
#include "../include/runewire_injector.h"

namespace
{
    void expect_true(bool condition, const char* message)
    {
        if (!condition)
        {
            throw message;
        }
    }

    void expect_equal(const char* actual, const char* expected, const char* message)
    {
        if ((actual == nullptr && expected != nullptr) || (actual != nullptr && expected == nullptr))
        {
            throw message;
        }

        if (actual && expected && std::strcmp(actual, expected) != 0)
        {
            throw message;
        }
    }

    int call_inject(const rw_injection_request& req, rw_injection_result& result)
    {
        return rw_inject(&req, &result);
    }
}

int main()
{
    try
    {
        // Unsupported technique should return failure and error code.
        rw_injection_request unsupported{};
        unsupported.recipe_name = "demo";
        unsupported.recipe_description = "desc";
        unsupported.technique_name = "Unknown";
        unsupported.technique_parameters_json = "{}";
        unsupported.payload_path = "C:\\\\payloads\\\\demo.dll";
        unsupported.allow_kernel_drivers = 0;
        unsupported.require_interactive_consent = 0;
        unsupported.target.kind = RW_TARGET_SELF;
        rw_injection_result unsupported_result{};

        int status = call_inject(unsupported, unsupported_result);
        expect_true(status != 0, "Unsupported technique should return non-zero status");
        expect_true(unsupported_result.success == 0, "Unsupported technique should set success = 0");
        expect_equal(unsupported_result.error_code, "TECHNIQUE_UNSUPPORTED", "Unsupported technique should set TECHNIQUE_UNSUPPORTED");

        // Invalid parameters JSON should fail.
        rw_injection_request bad_params = unsupported;
        bad_params.technique_name = "CreateRemoteThread"; // supported
        bad_params.technique_parameters_json = "[not json object]";
        rw_injection_result bad_params_result{};

        status = call_inject(bad_params, bad_params_result);
        expect_true(status != 0, "Bad params should return non-zero status");
        expect_true(bad_params_result.success == 0, "Bad params should set success = 0");
        expect_equal(bad_params_result.error_code, "TECHNIQUE_PARAMS_INVALID", "Bad params should set TECHNIQUE_PARAMS_INVALID");

        // Supported technique should succeed (stub) with minimal params.
        rw_injection_request ok = unsupported;
        ok.technique_name = "CreateRemoteThread";
        ok.technique_parameters_json = "{}";
        rw_injection_result ok_result{};

        status = call_inject(ok, ok_result);
        expect_true(status == 0, "Supported technique should return status 0");
        expect_true(ok_result.success != 0, "Supported technique should set success");

        // Hook technique with missing params should fail.
        rw_injection_request hook_missing = ok;
        hook_missing.technique_name = "InlineHook";
        hook_missing.technique_parameters_json = "{}";
        rw_injection_result hook_missing_result{};

        status = call_inject(hook_missing, hook_missing_result);
        expect_true(status != 0, "Hook missing params should fail");
        expect_equal(hook_missing_result.error_code, "TECHNIQUE_PARAM_REQUIRED", "Hook missing params should report TECHNIQUE_PARAM_REQUIRED");

        // Hook technique with params should return not implemented.
        rw_injection_request hook_stub = ok;
        hook_stub.technique_name = "InlineHook";
        hook_stub.technique_parameters_json = R"({"moduleName":"ws2_32.dll","functionName":"connect"})";
        rw_injection_result hook_stub_result{};

        status = call_inject(hook_stub, hook_stub_result);
        expect_true(status != 0, "Hook stub should fail with not implemented");
        expect_equal(hook_stub_result.error_code, "TECHNIQUE_NOT_IMPLEMENTED", "Hook stub should report TECHNIQUE_NOT_IMPLEMENTED");

        return 0;
    }
    catch (const char* msg)
    {
        // Simple test harness: print message and return failure.
        return 1;
    }
}
