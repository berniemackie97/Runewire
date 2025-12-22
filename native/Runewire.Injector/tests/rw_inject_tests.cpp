#include <cassert>
#include <cstring>
#include <cstdio>
#include <string>
#include <windows.h>
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

    std::string make_temp_file(const char* name, const unsigned char* data, size_t length)
    {
        char temp_dir[MAX_PATH] = {};
        ::GetTempPathA(MAX_PATH, temp_dir);
        std::string path = std::string(temp_dir) + name;
        FILE* f = nullptr;
        ::fopen_s(&f, path.c_str(), "wb");
        if (f)
        {
            if (data && length > 0)
            {
                std::fwrite(data, 1, length, f);
            }
            ::fclose(f);
        }
        return path;
    }

    std::string get_process_name()
    {
        char path[MAX_PATH] = {};
        ::GetModuleFileNameA(nullptr, path, MAX_PATH);
        const char* name = std::strrchr(path, '\\');
        if (name)
        {
            return std::string(name + 1);
        }
        return std::string(path);
    }

    std::string strip_exe_suffix(std::string value)
    {
        if (value.size() > 4)
        {
            const char* suffix = ".exe";
            const size_t offset = value.size() - 4;
            if (_stricmp(value.c_str() + offset, suffix) == 0)
            {
                value.resize(offset);
            }
        }
        return value;
    }

    DWORD WINAPI wait_thread_proc(LPVOID param)
    {
        HANDLE evt = static_cast<HANDLE>(param);
        if (evt)
        {
            ::WaitForSingleObject(evt, INFINITE);
        }
        return 0;
    }

    rw_injection_request make_base_request()
    {
        rw_injection_request req{};
        req.recipe_name = "demo";
        req.recipe_description = "desc";
        req.technique_name = "Unknown";
        req.technique_parameters_json = "{}";
        req.payload_path = "C:\\\\payloads\\\\demo.dll";
        req.allow_kernel_drivers = 0;
        req.require_interactive_consent = 0;
        req.target.kind = RW_TARGET_SELF;
        return req;
    }
}

int main()
{
    try
    {
        // Unsupported technique should return failure and error code.
        rw_injection_request unsupported = make_base_request();
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

        // Missing closing brace should fail.
        rw_injection_request bad_shape = bad_params;
        bad_shape.technique_parameters_json = R"({"key":"value")";
        rw_injection_result bad_shape_result{};
        status = call_inject(bad_shape, bad_shape_result);
        expect_true(status != 0, "Unterminated object should fail");
        expect_equal(bad_shape_result.error_code, "TECHNIQUE_PARAMS_INVALID", "Unterminated object should set TECHNIQUE_PARAMS_INVALID");

        // Supported technique should succeed (stub) with minimal params (using shellcode to avoid DLL requirement).
        rw_injection_request ok = unsupported;
        ok.technique_name = "Shellcode";
        ok.technique_parameters_json = "{}";
        rw_injection_result ok_result{};

        status = call_inject(ok, ok_result);
        expect_true(status != 0, "Shellcode without payload should fail");
        expect_equal(ok_result.error_code, "PAYLOAD_NOT_FOUND", "Shellcode missing payload");

        // CreateRemoteThread with current PID should succeed (reachability).
        char sys_dir[MAX_PATH] = {};
        ::GetSystemDirectoryA(sys_dir, MAX_PATH);
        std::string kernel32_path = std::string(sys_dir) + "\\\\kernel32.dll";

        rw_injection_request crt_self = make_base_request();
        crt_self.technique_name = "CreateRemoteThread";
        crt_self.target.kind = RW_TARGET_PROCESS_ID;
        crt_self.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        crt_self.payload_path = kernel32_path.c_str();
        rw_injection_result crt_self_result{};
        status = call_inject(crt_self, crt_self_result);
        expect_true(status == 0, "CreateRemoteThread current pid should succeed");
        expect_true(crt_self_result.success != 0, "CreateRemoteThread current pid success flag");

        // CreateRemoteThread with process name (with and without .exe) should succeed.
        const std::string process_name = get_process_name();
        const std::string process_stem = strip_exe_suffix(process_name);

        rw_injection_request crt_name = crt_self;
        crt_name.target.kind = RW_TARGET_PROCESS_NAME;
        crt_name.target.process_name = process_name.c_str();
        rw_injection_result crt_name_result{};
        status = call_inject(crt_name, crt_name_result);
        expect_true(status == 0, "CreateRemoteThread process name should succeed");
        expect_true(crt_name_result.success != 0, "CreateRemoteThread process name success flag");

        rw_injection_request crt_stem = crt_self;
        crt_stem.target.kind = RW_TARGET_PROCESS_NAME;
        crt_stem.target.process_name = process_stem.c_str();
        rw_injection_result crt_stem_result{};
        status = call_inject(crt_stem, crt_stem_result);
        expect_true(status == 0, "CreateRemoteThread process name without exe should succeed");
        expect_true(crt_stem_result.success != 0, "CreateRemoteThread process name without exe success flag");

        // CreateRemoteThread with missing DLL should fail.
        rw_injection_request crt_missing = crt_self;
        crt_missing.payload_path = "runewire_missing_crt.dll";
        rw_injection_result crt_missing_result{};
        status = call_inject(crt_missing, crt_missing_result);
        expect_true(status != 0, "CreateRemoteThread missing payload should fail");
        expect_equal(crt_missing_result.error_code, "PAYLOAD_NOT_FOUND", "CreateRemoteThread missing payload error");

        // CreateRemoteThread with bogus PID should fail to open.
        rw_injection_request crt_fail = crt_self;
        crt_fail.target.pid = 9999999;
        rw_injection_result crt_fail_result{};
        status = call_inject(crt_fail, crt_fail_result);
        expect_true(status != 0, "CreateRemoteThread bogus pid should fail");
        expect_equal(crt_fail_result.error_code, "TARGET_OPEN_FAILED", "CreateRemoteThread bogus pid error");

        // QueueUserAPC with current PID should succeed (reachability).
        rw_injection_request apc_self = make_base_request();
        apc_self.technique_name = "QueueUserAPC";
        apc_self.target.kind = RW_TARGET_PROCESS_ID;
        apc_self.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        const unsigned char apc_bytes[] = { 0x90, 0x90, 0xC3 };
        const std::string temp_apc_path = make_temp_file("runewire_temp_apc.bin", apc_bytes, sizeof(apc_bytes));
        apc_self.payload_path = temp_apc_path.c_str();
        rw_injection_result apc_self_result{};
        status = call_inject(apc_self, apc_self_result);
        if (status != 0 && apc_self_result.error_code)
        {
            std::printf("QueueUserAPC self error: %s\n", apc_self_result.error_code);
        }
        expect_true(status == 0, "QueueUserAPC current pid should succeed");
        expect_true(apc_self_result.success != 0, "QueueUserAPC current pid success flag");

        // QueueUserAPC with invalid threadId should fail.
        rw_injection_request apc_invalid_thread = apc_self;
        apc_invalid_thread.technique_parameters_json = R"({"threadId":0})";
        rw_injection_result apc_invalid_thread_result{};
        status = call_inject(apc_invalid_thread, apc_invalid_thread_result);
        expect_true(status != 0, "QueueUserAPC invalid threadId should fail");
        expect_equal(apc_invalid_thread_result.error_code, "TECHNIQUE_PARAM_INVALID", "QueueUserAPC invalid threadId");

        // QueueUserAPC with negative timeout should fail.
        rw_injection_request apc_invalid_timeout = apc_self;
        apc_invalid_timeout.technique_parameters_json = R"({"timeoutMs":-1})";
        rw_injection_result apc_invalid_timeout_result{};
        status = call_inject(apc_invalid_timeout, apc_invalid_timeout_result);
        expect_true(status != 0, "QueueUserAPC negative timeout should fail");
        expect_equal(apc_invalid_timeout_result.error_code, "TECHNIQUE_PARAM_INVALID", "QueueUserAPC negative timeout");

        // QueueUserAPC with explicit threadId that can be opened should succeed.
        char thread_param[64];
        std::snprintf(thread_param, sizeof(thread_param), R"({"threadId":%lu})", ::GetCurrentThreadId());
        rw_injection_request apc_thread = apc_self;
        apc_thread.technique_parameters_json = thread_param;
        rw_injection_result apc_thread_result{};
        status = call_inject(apc_thread, apc_thread_result);
        expect_true(status == 0, "QueueUserAPC current thread should succeed");
        expect_true(apc_thread_result.success != 0, "QueueUserAPC current thread success flag");

        // QueueUserAPC with non-existent thread should fail.
        rw_injection_request apc_thread_fail = apc_self;
        apc_thread_fail.technique_parameters_json = R"({"threadId":999999})";
        rw_injection_result apc_thread_fail_result{};
        status = call_inject(apc_thread_fail, apc_thread_fail_result);
        expect_true(status != 0, "QueueUserAPC invalid thread should fail");
        expect_equal(apc_thread_fail_result.error_code, "THREAD_OPEN_FAILED", "QueueUserAPC invalid thread error");

        // QueueUserAPC bogus PID should fail to open.
        rw_injection_request apc_fail = apc_self;
        apc_fail.target.pid = 9999999;
        rw_injection_result apc_fail_result{};
        status = call_inject(apc_fail, apc_fail_result);
        expect_true(status != 0, "QueueUserAPC bogus pid should fail");
        expect_equal(apc_fail_result.error_code, "TARGET_OPEN_FAILED", "QueueUserAPC bogus pid error");
        ::DeleteFileA(temp_apc_path.c_str());

        // NtCreateThreadEx with current PID should succeed (reachability).
        rw_injection_request nt_self = make_base_request();
        nt_self.technique_name = "NtCreateThreadEx";
        nt_self.target.kind = RW_TARGET_PROCESS_ID;
        nt_self.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        nt_self.payload_path = kernel32_path.c_str();
        rw_injection_result nt_self_result{};
        status = call_inject(nt_self, nt_self_result);
        expect_true(status == 0, "NtCreateThreadEx current pid should succeed");
        expect_true(nt_self_result.success != 0, "NtCreateThreadEx current pid success flag");

        // NtCreateThreadEx bogus PID should fail to open.
        rw_injection_request nt_fail = nt_self;
        nt_fail.target.pid = 9999999;
        rw_injection_result nt_fail_result{};
        status = call_inject(nt_fail, nt_fail_result);
        expect_true(status != 0, "NtCreateThreadEx bogus pid should fail");
        expect_equal(nt_fail_result.error_code, "TARGET_OPEN_FAILED", "NtCreateThreadEx bogus pid error");

        // NtCreateThreadEx with negative creationFlags should fail.
        rw_injection_request nt_flags = nt_self;
        nt_flags.technique_parameters_json = R"({"creationFlags":-1})";
        rw_injection_result nt_flags_result{};
        status = call_inject(nt_flags, nt_flags_result);
        expect_true(status != 0, "NtCreateThreadEx negative creationFlags should fail");
        expect_equal(nt_flags_result.error_code, "TECHNIQUE_PARAM_INVALID", "NtCreateThreadEx invalid creationFlags");

        HANDLE hijack_event = ::CreateEventA(nullptr, TRUE, FALSE, nullptr);
        expect_true(hijack_event != nullptr, "ThreadHijack event should be created");
        DWORD hijack_thread_id = 0;
        HANDLE hijack_thread = ::CreateThread(nullptr, 0, wait_thread_proc, hijack_event, 0, &hijack_thread_id);
        expect_true(hijack_thread != nullptr, "ThreadHijack thread should be created");

        // ThreadHijack with current PID should succeed.
        rw_injection_request hijack_self = make_base_request();
        hijack_self.technique_name = "ThreadHijack";
        hijack_self.target.kind = RW_TARGET_PROCESS_ID;
        hijack_self.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        hijack_self.payload_path = kernel32_path.c_str();
        char hijack_params[64];
        std::snprintf(hijack_params, sizeof(hijack_params), R"({"threadId":%lu})", hijack_thread_id);
        hijack_self.technique_parameters_json = hijack_params;
        rw_injection_result hijack_self_result{};
        status = call_inject(hijack_self, hijack_self_result);
        expect_true(status == 0, "ThreadHijack current pid should succeed");
        expect_true(hijack_self_result.success != 0, "ThreadHijack current pid success flag");

        // ThreadHijack with invalid threadId should fail.
        rw_injection_request hijack_invalid = hijack_self;
        hijack_invalid.technique_parameters_json = R"({"threadId":0})";
        rw_injection_result hijack_invalid_result{};
        status = call_inject(hijack_invalid, hijack_invalid_result);
        expect_true(status != 0, "ThreadHijack invalid threadId should fail");
        expect_equal(hijack_invalid_result.error_code, "TECHNIQUE_PARAM_INVALID", "ThreadHijack invalid threadId error");

        // ThreadHijack bogus PID should fail.
        rw_injection_request hijack_fail = hijack_self;
        hijack_fail.target.pid = 9999999;
        rw_injection_result hijack_fail_result{};
        status = call_inject(hijack_fail, hijack_fail_result);
        expect_true(status != 0, "ThreadHijack bogus pid should fail");
        expect_equal(hijack_fail_result.error_code, "TARGET_OPEN_FAILED", "ThreadHijack bogus pid error");

        ::SetEvent(hijack_event);
        ::WaitForSingleObject(hijack_thread, 2000);
        ::CloseHandle(hijack_thread);
        ::CloseHandle(hijack_event);

        // ManualMap missing payloadPath param should fall back to request path and fail when not found.
        rw_injection_request mm_missing = make_base_request();
        mm_missing.technique_name = "ManualMap";
        mm_missing.target.kind = RW_TARGET_PROCESS_ID;
        mm_missing.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        mm_missing.technique_parameters_json = "{}";
        rw_injection_result mm_missing_result{};
        status = call_inject(mm_missing, mm_missing_result);
        expect_true(status != 0, "ManualMap missing payloadPath should fail");
        expect_equal(mm_missing_result.error_code, "PAYLOAD_NOT_FOUND", "ManualMap missing payloadPath uses request payload");

        // ManualMap with payloadPath should fail without reflective export.
        const unsigned char dummy_bytes[] = { 0x00 };
        const std::string temp_path = make_temp_file("runewire_temp_dummy.bin", dummy_bytes, sizeof(dummy_bytes));
        rw_injection_request mm_ok = mm_missing;
        mm_ok.payload_path = temp_path.c_str();
        mm_ok.technique_parameters_json = "{}";
        rw_injection_result mm_ok_result{};
        status = call_inject(mm_ok, mm_ok_result);
        expect_true(status != 0, "ManualMap without reflective export should fail");
        expect_equal(mm_ok_result.error_code, "REFLECTIVE_EXPORT_NOT_FOUND", "ManualMap missing reflective export");
        ::DeleteFileA(temp_path.c_str());

        // ManualMap with missing payload should fail with PAYLOAD_NOT_FOUND.
        rw_injection_request mm_missing_file = mm_missing;
        mm_missing_file.technique_parameters_json = R"({"payloadPath":"runewire_missing.bin"})";
        rw_injection_result mm_missing_file_result{};
        status = call_inject(mm_missing_file, mm_missing_file_result);
        expect_true(status != 0, "ManualMap missing file should fail");
        expect_equal(mm_missing_file_result.error_code, "PAYLOAD_NOT_FOUND", "ManualMap missing file error");

        // Shellcode uses request payload path by default; missing file should fail.
        rw_injection_request sc_missing = make_base_request();
        sc_missing.technique_name = "Shellcode";
        sc_missing.target.kind = RW_TARGET_PROCESS_ID;
        sc_missing.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        sc_missing.payload_path = "runewire_missing_sc.bin";
        rw_injection_result sc_missing_result{};
        status = call_inject(sc_missing, sc_missing_result);
        expect_true(status != 0, "Shellcode missing payload should fail");
        expect_equal(sc_missing_result.error_code, "PAYLOAD_NOT_FOUND", "Shellcode missing payload");

        // Shellcode with override payloadPath and existing file should succeed (stub).
        const unsigned char shellcode_bytes[] = { 0x90, 0x90, 0xC3 }; // NOP; NOP; RET
        const std::string temp_sc_path = make_temp_file("runewire_temp_sc.bin", shellcode_bytes, sizeof(shellcode_bytes));
        rw_injection_request sc_ok = sc_missing;
        sc_ok.payload_path = temp_sc_path.c_str();
        sc_ok.technique_parameters_json = "{}";
        rw_injection_result sc_ok_result{};
        status = call_inject(sc_ok, sc_ok_result);
        expect_true(status == 0, "Shellcode with payload should succeed");
        expect_true(sc_ok_result.success != 0, "Shellcode success flag");

        // Shellcode with entryOffset should succeed.
        rw_injection_request sc_offset_ok = sc_ok;
        sc_offset_ok.technique_parameters_json = R"({"entryOffset":1})";
        rw_injection_result sc_offset_ok_result{};
        status = call_inject(sc_offset_ok, sc_offset_ok_result);
        expect_true(status == 0, "Shellcode with entryOffset should succeed");

        // Shellcode with invalid entryOffset should fail.
        rw_injection_request sc_offset_bad = sc_ok;
        sc_offset_bad.technique_parameters_json = R"({"entryOffset":999})";
        rw_injection_result sc_offset_bad_result{};
        status = call_inject(sc_offset_bad, sc_offset_bad_result);
        expect_true(status != 0, "Shellcode invalid entryOffset should fail");
        expect_equal(sc_offset_bad_result.error_code, "TECHNIQUE_PARAM_INVALID", "Shellcode invalid entryOffset");

        // Shellcode with negative entryOffset should fail.
        rw_injection_request sc_offset_negative = sc_ok;
        sc_offset_negative.technique_parameters_json = R"({"entryOffset":-1})";
        rw_injection_result sc_offset_negative_result{};
        status = call_inject(sc_offset_negative, sc_offset_negative_result);
        expect_true(status != 0, "Shellcode negative entryOffset should fail");
        expect_equal(sc_offset_negative_result.error_code, "TECHNIQUE_PARAM_INVALID", "Shellcode negative entryOffset");
        ::DeleteFileA(temp_sc_path.c_str());

        // ReflectiveDll missing payload should fail.
        rw_injection_request rdi_missing = make_base_request();
        rdi_missing.technique_name = "ReflectiveDll";
        rdi_missing.target.kind = RW_TARGET_PROCESS_ID;
        rdi_missing.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        rdi_missing.payload_path = "runewire_missing_rdi.dll";
        rw_injection_result rdi_missing_result{};
        status = call_inject(rdi_missing, rdi_missing_result);
        expect_true(status != 0, "ReflectiveDll missing payload should fail");
        expect_equal(rdi_missing_result.error_code, "PAYLOAD_NOT_FOUND", "ReflectiveDll missing payload");

        // ReflectiveDll with existing file should fail without reflective export.
        const std::string temp_rdi_path = make_temp_file("runewire_temp_rdi.dll", dummy_bytes, sizeof(dummy_bytes));
        rw_injection_request rdi_ok = rdi_missing;
        rdi_ok.payload_path = temp_rdi_path.c_str(); // use created file
        rdi_ok.technique_parameters_json = "{}";
        rw_injection_result rdi_ok_result{};
        status = call_inject(rdi_ok, rdi_ok_result);
        expect_true(status != 0, "ReflectiveDll without reflective export should fail");
        expect_equal(rdi_ok_result.error_code, "REFLECTIVE_EXPORT_NOT_FOUND", "ReflectiveDll missing reflective export");
        ::DeleteFileA(temp_rdi_path.c_str());

        // ModuleStomping missing payload should fail.
        rw_injection_request stomp_missing = make_base_request();
        stomp_missing.technique_name = "ModuleStomping";
        stomp_missing.target.kind = RW_TARGET_PROCESS_ID;
        stomp_missing.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        stomp_missing.payload_path = "runewire_missing_stomp.dll";
        rw_injection_result stomp_missing_result{};
        status = call_inject(stomp_missing, stomp_missing_result);
        expect_true(status != 0, "ModuleStomping missing payload should fail");
        expect_equal(stomp_missing_result.error_code, "PAYLOAD_NOT_FOUND", "ModuleStomping missing payload");

        // ModuleStomping with payload should reject self target.
        const unsigned char stomp_bytes[] = { 0xC3 };
        const std::string temp_stomp_path = make_temp_file("runewire_temp_stomp.dll", stomp_bytes, sizeof(stomp_bytes));
        rw_injection_request stomp_ok = stomp_missing;
        stomp_ok.payload_path = temp_stomp_path.c_str();
        rw_injection_result stomp_ok_result{};
        status = call_inject(stomp_ok, stomp_ok_result);
        expect_true(status != 0, "ModuleStomping self should fail");
        expect_equal(stomp_ok_result.error_code, "TARGET_SELF_UNSUPPORTED", "ModuleStomping self target error");
        ::DeleteFileA(temp_stomp_path.c_str());

        // SharedSectionMap missing payload should fail.
        rw_injection_request ssm_missing = make_base_request();
        ssm_missing.technique_name = "SharedSectionMap";
        ssm_missing.target.kind = RW_TARGET_PROCESS_ID;
        ssm_missing.target.pid = static_cast<unsigned long>(::GetCurrentProcessId());
        ssm_missing.payload_path = "runewire_missing_ssm.bin";
        rw_injection_result ssm_missing_result{};
        status = call_inject(ssm_missing, ssm_missing_result);
        expect_true(status != 0, "SharedSectionMap missing payload should fail");
        expect_equal(ssm_missing_result.error_code, "PAYLOAD_NOT_FOUND", "SharedSectionMap missing payload");

        // SharedSectionMap with payload should succeed.
        const unsigned char ssm_bytes[] = { 0xC3 };
        const std::string temp_ssm_path = make_temp_file("runewire_temp_ssm.bin", ssm_bytes, sizeof(ssm_bytes));
        rw_injection_request ssm_ok = ssm_missing;
        ssm_ok.payload_path = temp_ssm_path.c_str();
        rw_injection_result ssm_ok_result{};
        status = call_inject(ssm_ok, ssm_ok_result);
        expect_true(status == 0, "SharedSectionMap with payload should succeed");
        expect_true(ssm_ok_result.success != 0, "SharedSectionMap success flag");
        ::DeleteFileA(temp_ssm_path.c_str());

        // HttpRedirect missing param fails
        rw_injection_request http_missing = ok;
        http_missing.technique_name = "HttpRedirect";
        http_missing.technique_parameters_json = "{}";
        rw_injection_result http_missing_result{};
        status = call_inject(http_missing, http_missing_result);
        expect_true(status != 0, "HttpRedirect missing targetUrl should fail");
        expect_equal(http_missing_result.error_code, "TECHNIQUE_PARAM_REQUIRED", "HttpRedirect missing targetUrl");

        // HttpRedirect with params is not implemented
        rw_injection_request http_stub = ok;
        http_stub.technique_name = "HttpRedirect";
        http_stub.technique_parameters_json = R"({"targetUrl":"https://example.com"})";
        rw_injection_result http_stub_result{};
        status = call_inject(http_stub, http_stub_result);
        expect_true(status != 0, "HttpRedirect not implemented");
        expect_equal(http_stub_result.error_code, "TECHNIQUE_NOT_IMPLEMENTED", "HttpRedirect not implemented code");

        // EarlyBirdCreateProcess requires commandLine
        rw_injection_request eb_missing = ok;
        eb_missing.technique_name = "EarlyBirdCreateProcess";
        eb_missing.technique_parameters_json = "{}";
        rw_injection_result eb_missing_result{};
        status = call_inject(eb_missing, eb_missing_result);
        expect_true(status != 0, "EarlyBirdCreateProcess missing param");
        expect_equal(eb_missing_result.error_code, "TECHNIQUE_PARAM_REQUIRED", "EarlyBirdCreateProcess missing commandLine");

        // EarlyBirdCreateProcess empty commandLine should also fail.
        rw_injection_request eb_empty = ok;
        eb_empty.technique_name = "EarlyBirdCreateProcess";
        eb_empty.technique_parameters_json = R"({"commandLine":""})";
        rw_injection_result eb_empty_result{};
        status = call_inject(eb_empty, eb_empty_result);
        expect_true(status != 0, "EarlyBirdCreateProcess empty commandLine");
        expect_equal(eb_empty_result.error_code, "TECHNIQUE_PARAM_REQUIRED", "EarlyBirdCreateProcess empty commandLine");

        // ProcessHollowing missing targetImagePath should fail.
        rw_injection_request hollow_missing = ok;
        hollow_missing.technique_name = "ProcessHollowing";
        hollow_missing.technique_parameters_json = "{}";
        rw_injection_result hollow_missing_result{};
        status = call_inject(hollow_missing, hollow_missing_result);
        expect_true(status != 0, "ProcessHollowing missing target image should fail");
        expect_equal(hollow_missing_result.error_code, "TECHNIQUE_PARAM_REQUIRED", "ProcessHollowing missing target image");

        // ProcessHollowing missing target image should fail.
        const unsigned char hollow_bytes[] = { 0x00 };
        const std::string temp_hollow_path = make_temp_file("runewire_temp_hollow.bin", hollow_bytes, sizeof(hollow_bytes));
        rw_injection_request hollow_missing_target = ok;
        hollow_missing_target.technique_name = "ProcessHollowing";
        hollow_missing_target.payload_path = temp_hollow_path.c_str();
        hollow_missing_target.technique_parameters_json = R"({"targetImagePath":"C:\\\\runewire_missing_target.exe"})";
        rw_injection_result hollow_missing_target_result{};
        status = call_inject(hollow_missing_target, hollow_missing_target_result);
        expect_true(status != 0, "ProcessHollowing missing target image should fail");
        expect_equal(hollow_missing_target_result.error_code, "TARGET_IMAGE_NOT_FOUND", "ProcessHollowing missing target image");
        ::DeleteFileA(temp_hollow_path.c_str());

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

        // InlineHook with empty moduleName should fail param required.
        rw_injection_request hook_empty_param = ok;
        hook_empty_param.technique_name = "InlineHook";
        hook_empty_param.technique_parameters_json = R"({"moduleName":"","functionName":"connect"})";
        rw_injection_result hook_empty_result{};
        status = call_inject(hook_empty_param, hook_empty_result);
        expect_true(status != 0, "Hook with empty moduleName should fail");
        expect_equal(hook_empty_result.error_code, "TECHNIQUE_PARAM_REQUIRED", "Hook empty param should report TECHNIQUE_PARAM_REQUIRED");

        // WinsockRedirect with non-numeric port should fail param invalid.
        rw_injection_request win_redirect = ok;
        win_redirect.technique_name = "WinsockRedirect";
        win_redirect.technique_parameters_json = R"({"targetHost":"example.com","targetPort":"abc"})";
        rw_injection_result win_redirect_result{};
        status = call_inject(win_redirect, win_redirect_result);
        expect_true(status != 0, "WinsockRedirect non-numeric port should fail");
        expect_equal(win_redirect_result.error_code, "TECHNIQUE_PARAM_INVALID", "WinsockRedirect non-numeric port");

        // WinsockRedirect with out-of-range port should fail param invalid.
        rw_injection_request win_redirect_range = ok;
        win_redirect_range.technique_name = "WinsockRedirect";
        win_redirect_range.technique_parameters_json = R"({"targetHost":"example.com","targetPort":"70000"})";
        rw_injection_result win_redirect_range_result{};
        status = call_inject(win_redirect_range, win_redirect_range_result);
        expect_true(status != 0, "WinsockRedirect port out of range should fail");
        expect_equal(win_redirect_range_result.error_code, "TECHNIQUE_PARAM_INVALID", "WinsockRedirect out of range");

        // WinsockRedirect with numeric (unquoted) port should parse and hit not implemented.
        rw_injection_request win_redirect_numeric = ok;
        win_redirect_numeric.technique_name = "WinsockRedirect";
        win_redirect_numeric.technique_parameters_json = R"({"targetHost":"example.com","targetPort":8080})";
        rw_injection_result win_redirect_numeric_result{};
        status = call_inject(win_redirect_numeric, win_redirect_numeric_result);
        expect_true(status != 0, "WinsockRedirect numeric port should pass validation then not implemented");
        expect_equal(win_redirect_numeric_result.error_code, "TECHNIQUE_NOT_IMPLEMENTED", "WinsockRedirect numeric port not implemented");

        // HttpRedirect invalid scheme should fail param invalid.
        rw_injection_request http_invalid = ok;
        http_invalid.technique_name = "HttpRedirect";
        http_invalid.technique_parameters_json = R"({"targetUrl":"ftp://example.com"})";
        rw_injection_result http_invalid_result{};
        status = call_inject(http_invalid, http_invalid_result);
        expect_true(status != 0, "HttpRedirect invalid scheme should fail");
        expect_equal(http_invalid_result.error_code, "TECHNIQUE_PARAM_INVALID", "HttpRedirect invalid scheme");

        return 0;
    }
    catch (const char* msg)
    {
        // Simple test harness: print message and return failure.
        std::puts(msg);
        return 1;
    }
}
