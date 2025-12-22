#pragma once

#include "param_parser.h"
#include "technique_dispatch.h"

dispatch_outcome handle_process_hollowing(const rw_injection_request* req, const parsed_params& params);
