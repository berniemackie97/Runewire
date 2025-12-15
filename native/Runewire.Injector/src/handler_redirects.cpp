#include "handler_redirects.h"

#include <string>

namespace
{
    std::optional<std::string> get_required_string(const parsed_params& params, const char* key, const char* required_message)
    {
        const auto value = params.get_string(key);
        if (!value.has_value() || value->empty())
        {
            return std::nullopt;
        }
        return value;
    }

    dispatch_outcome param_required(const char* message)
    {
        return { false, "TECHNIQUE_PARAM_REQUIRED", message };
    }

    dispatch_outcome param_invalid(const char* message)
    {
        return { false, "TECHNIQUE_PARAM_INVALID", message };
    }

    dispatch_outcome not_implemented(const char* message)
    {
        return { false, "TECHNIQUE_NOT_IMPLEMENTED", message };
    }

    bool parse_required_int_range(const parsed_params& params,
        const char* key,
        int min_value,
        int max_value,
        int& out_value,
        const char* required_message,
        const char* numeric_message,
        const char* range_message,
        dispatch_outcome& failure)
    {
        const auto value_opt = params.get_int(key);
        if (!value_opt.has_value())
        {
            failure = params.has_non_empty(key) ? param_invalid(numeric_message) : param_required(required_message);
            return false;
        }

        out_value = *value_opt;

        if (out_value < min_value || out_value > max_value)
        {
            failure = param_invalid(range_message);
            return false;
        }

        return true;
    }
}

dispatch_outcome handle_inline_hook(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "moduleName", "InlineHook requires moduleName and functionName parameters."))
    {
        return param_required("InlineHook requires moduleName and functionName parameters.");
    }
    if (!get_required_string(params, "functionName", "InlineHook requires moduleName and functionName parameters."))
    {
        return param_required("InlineHook requires moduleName and functionName parameters.");
    }
    return not_implemented("InlineHook is not implemented in this build.");
}

dispatch_outcome handle_iat_hook(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "moduleName", "IatHook requires moduleName and importName parameters.") ||
        !get_required_string(params, "importName", "IatHook requires moduleName and importName parameters."))
    {
        return param_required("IatHook requires moduleName and importName parameters.");
    }
    return not_implemented("IatHook is not implemented in this build.");
}

dispatch_outcome handle_eat_hook(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "moduleName", "EatHook requires moduleName and exportName parameters.") ||
        !get_required_string(params, "exportName", "EatHook requires moduleName and exportName parameters."))
    {
        return param_required("EatHook requires moduleName and exportName parameters.");
    }
    return not_implemented("EatHook is not implemented in this build.");
}

dispatch_outcome handle_winsock_redirect(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "targetHost", "WinsockRedirect requires targetHost and targetPort parameters."))
    {
        return param_required("WinsockRedirect requires targetHost and targetPort parameters.");
    }

    dispatch_outcome failure{};
    int port = 0;
    if (!parse_required_int_range(params, "targetPort", 1, 65535, port,
            "WinsockRedirect requires targetHost and targetPort parameters.",
            "WinsockRedirect targetPort must be numeric.",
            "WinsockRedirect targetPort must be 1-65535.",
            failure))
    {
        return failure;
    }
    return not_implemented("WinsockRedirect is not implemented in this build.");
}

dispatch_outcome handle_http_redirect(const rw_injection_request*, const parsed_params& params)
{
    const auto url = params.get_string("targetUrl");
    if (!url.has_value() || url->empty())
    {
        return param_required("HttpRedirect requires targetUrl parameter.");
    }
    if (!(url->rfind("http://", 0) == 0 || url->rfind("https://", 0) == 0))
    {
        return param_invalid("HttpRedirect targetUrl must start with http:// or https://");
    }
    return not_implemented("HttpRedirect is not implemented in this build.");
}

dispatch_outcome handle_dns_override(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "host", "DnsOverride requires host and address parameters.") ||
        !get_required_string(params, "address", "DnsOverride requires host and address parameters."))
    {
        return param_required("DnsOverride requires host and address parameters.");
    }
    return not_implemented("DnsOverride is not implemented in this build.");
}

dispatch_outcome handle_fs_redirect(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "targetPath", "FileSystemRedirect requires targetPath and redirectPath parameters.") ||
        !get_required_string(params, "redirectPath", "FileSystemRedirect requires targetPath and redirectPath parameters."))
    {
        return param_required("FileSystemRedirect requires targetPath and redirectPath parameters.");
    }
    return not_implemented("FileSystemRedirect is not implemented in this build.");
}

dispatch_outcome handle_tls_bypass(const rw_injection_request*, const parsed_params&)
{
    return not_implemented("TlsBypass is not implemented in this build.");
}

dispatch_outcome handle_early_bird(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "commandLine", "Early bird techniques require commandLine parameter."))
    {
        return param_required("Early bird techniques require commandLine parameter.");
    }
    return not_implemented("Early bird techniques are not implemented in this build.");
}

dispatch_outcome handle_section_copy(const rw_injection_request*, const parsed_params&)
{
    return not_implemented("SectionCopyExecute is not implemented in this build.");
}

dispatch_outcome handle_threadpool_apc(const rw_injection_request*, const parsed_params&)
{
    return not_implemented("ThreadpoolApc is not implemented in this build.");
}

dispatch_outcome handle_module_stomp_restore(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "moduleName", "ModuleStompRestore requires moduleName parameter."))
    {
        return param_required("ModuleStompRestore requires moduleName parameter.");
    }
    return not_implemented("ModuleStompRestore is not implemented in this build.");
}

dispatch_outcome handle_call_export_init(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "exportName", "CallExportInit requires exportName parameter."))
    {
        return param_required("CallExportInit requires exportName parameter.");
    }
    return not_implemented("CallExportInit is not implemented in this build.");
}

dispatch_outcome handle_preload_launch(const rw_injection_request*, const parsed_params& params)
{
    if (!get_required_string(params, "libraryPath", "Preload techniques require libraryPath and commandLine parameters.") ||
        !get_required_string(params, "commandLine", "Preload techniques require libraryPath and commandLine parameters."))
    {
        return param_required("Preload techniques require libraryPath and commandLine parameters.");
    }
    return not_implemented("Preload launch techniques are not implemented in this build.");
}

dispatch_outcome handle_not_implemented(const rw_injection_request*, const parsed_params&)
{
    return not_implemented("Technique is not implemented in this build.");
}
