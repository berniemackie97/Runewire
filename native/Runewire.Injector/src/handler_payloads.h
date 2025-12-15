#pragma once

#include "param_parser.h"
#include "technique_dispatch.h"

dispatch_outcome handle_manual_map(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_shellcode(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_reflective_dll(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_module_stomping(const rw_injection_request* req, const parsed_params& params);
dispatch_outcome handle_shared_section_map(const rw_injection_request* req, const parsed_params& params);
