#include "technique_dispatch.h"

#include <cctype>
#include <string>

#include "handler_payloads.h"
#include "handler_redirects.h"
#include "handler_threads.h"
#include "param_parser.h"

namespace
{
    bool equals_ignore_case(const char* a, const char* b)
    {
        if (!a || !b)
        {
            return false;
        }

        while (*a && *b)
        {
            if (std::tolower(static_cast<unsigned char>(*a)) != std::tolower(static_cast<unsigned char>(*b)))
            {
                return false;
            }
            ++a;
            ++b;
        }

        return *a == '\0' && *b == '\0';
    }

    dispatch_outcome params_invalid()
    {
        return { false, "TECHNIQUE_PARAMS_INVALID", "Technique parameters must be a JSON object." };
    }

    dispatch_outcome ok()
    {
        return { true, nullptr, nullptr };
    }

    using technique_handler = dispatch_outcome (*)(const rw_injection_request*, const parsed_params&);

    dispatch_outcome handle_stubbed(const rw_injection_request*, const parsed_params&)
    {
        return ok();
    }

    struct technique_entry
    {
        const char* name;
        technique_handler handler;
    };

    constexpr technique_entry techniques[] = {
        // Stubbed/supported in this build.
        { "CreateRemoteThread", handle_create_remote_thread },
        { "QueueUserAPC", handle_queue_user_apc },
        { "NtCreateThreadEx", handle_nt_create_thread_ex },
        { "ManualMap", handle_manual_map },
        { "Shellcode", handle_shellcode },
        { "ThreadHijack", handle_thread_hijack },
        { "EarlyBirdApc", handle_stubbed },
        { "ProcessHollowing", handle_stubbed },
        { "ProcessDoppelganging", handle_stubbed },
        { "ProcessHerpaderping", handle_stubbed },
        { "ModuleStomping", handle_module_stomping },
        { "SharedSectionMap", handle_shared_section_map },
        { "ReflectiveDll", handle_reflective_dll },
        { "ClrHost", handle_stubbed },
        { "PtraceInject", handle_stubbed },
        { "MemfdShellcode", handle_stubbed },
        { "MachThreadInject", handle_stubbed },

        // Hooks/redirects.
        { "InlineHook", handle_inline_hook },
        { "IatHook", handle_iat_hook },
        { "EatHook", handle_eat_hook },
        { "WinsockRedirect", handle_winsock_redirect },
        { "HttpRedirect", handle_http_redirect },
        { "DnsOverride", handle_dns_override },
        { "FileSystemRedirect", handle_fs_redirect },
        { "TlsBypass", handle_tls_bypass },

        // Early bird and related.
        { "EarlyBirdCreateProcess", handle_early_bird },
        { "EarlyBirdQueueApc", handle_early_bird },
        { "SectionCopyExecute", handle_section_copy },
        { "ThreadpoolApc", handle_threadpool_apc },
        { "ModuleStompRestore", handle_module_stomp_restore },
        { "CallExportInit", handle_call_export_init },
        { "LdPreloadLaunch", handle_preload_launch },
        { "DyldInsertLaunch", handle_preload_launch },

        // Not implemented fallbacks.
        { "PtraceThreadHijack", handle_not_implemented },
        { "MemoryScanPatch", handle_not_implemented },
        { "AntiHookDetect", handle_not_implemented },
        { "SnapshotRestore", handle_not_implemented },
    };
}

dispatch_outcome dispatch_technique(const rw_injection_request* req)
{
    if (!req)
    {
        return { false, "NULL_REQUEST", "Injection request pointer was null." };
    }

    parsed_params params{};
    if (!parse_params_object(req->technique_parameters_json, params))
    {
        return params_invalid();
    }

    for (const auto& entry : techniques)
    {
        if (equals_ignore_case(req->technique_name, entry.name))
        {
            return entry.handler(req, params);
        }
    }

    return { false, "TECHNIQUE_UNSUPPORTED", "Technique is not implemented in this build." };
}
