#include "param_parser.h"

bool parsed_params::has_non_empty(const char* key) const
{
    if (!key || !root.is_object())
    {
        return false;
    }
    const auto it = root.find(key);
    if (it == root.end() || it->is_null())
    {
        return false;
    }
    if (it->is_string())
    {
        return !it->get_ref<const std::string&>().empty();
    }
    return true;
}

std::optional<std::string> parsed_params::get_string(const char* key) const
{
    if (!key || !root.is_object())
    {
        return std::nullopt;
    }
    const auto it = root.find(key);
    if (it == root.end() || it->is_null())
    {
        return std::nullopt;
    }
    if (it->is_string())
    {
        return it->get<std::string>();
    }
    if (it->is_number_integer() || it->is_number_unsigned())
    {
        return std::to_string(it->get<long long>());
    }
    if (it->is_boolean())
    {
        return it->get<bool>() ? "true" : "false";
    }
    return std::nullopt;
}

std::optional<int> parsed_params::get_int(const char* key) const
{
    if (!key || !root.is_object())
    {
        return std::nullopt;
    }
    const auto it = root.find(key);
    if (it == root.end() || it->is_null())
    {
        return std::nullopt;
    }
    if (it->is_number_integer() || it->is_number_unsigned())
    {
        return static_cast<int>(it->get<long long>());
    }
    if (it->is_string())
    {
        try
        {
            return std::stoi(it->get<std::string>());
        }
        catch (...)
        {
            return std::nullopt;
        }
    }
    return std::nullopt;
}

bool parse_params_object(const char* json, parsed_params& out)
{
    if (!json || json[0] == '\0')
    {
        out.root = nlohmann::json::object();
        return true;
    }

    try
    {
        out.root = nlohmann::json::parse(json);
    }
    catch (...)
    {
        return false;
    }

    return out.root.is_object();
}
