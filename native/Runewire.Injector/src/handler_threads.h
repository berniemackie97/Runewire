#pragma once

#include "param_parser.h"
#include "technique_dispatch.h"

dispatch_outcome handle_create_remote_thread(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_queue_user_apc(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_nt_create_thread_ex(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_thread_hijack(const rw_injection_request* req, const parsed_params& params);
