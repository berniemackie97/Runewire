#pragma once

#include <optional>
#include <string>
#include <unordered_map>

#include <nlohmann/json.hpp>

struct parsed_params
{
    nlohmann::json root;

    bool has_non_empty(const char* key) const;
    std::optional<std::string> get_string(const char* key) const;
    std::optional<int> get_int(const char* key) const;
};

// Parses a JSON object; returns false on invalid JSON or non-object top level.
bool parse_params_object(const char* json, parsed_params& out);
