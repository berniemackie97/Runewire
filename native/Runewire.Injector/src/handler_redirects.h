#pragma once

#include "param_parser.h"
#include "technique_dispatch.h"

dispatch_outcome handle_inline_hook(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_iat_hook(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_eat_hook(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_winsock_redirect(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_http_redirect(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_dns_override(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_fs_redirect(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_tls_bypass(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_early_bird(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_section_copy(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_threadpool_apc(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_module_stomp_restore(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_call_export_init(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_preload_launch(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_not_implemented(const rw_injection_request* req, const parsed_params& params);
