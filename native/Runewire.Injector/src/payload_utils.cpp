#include "payload_utils.h"

#include <filesystem>
#include <fstream>
#include <vector>

bool payload_exists(const char* path)
{
    if (!path || path[0] == '\0')
    {
        return false;
    }

    std::error_code ec;
    std::filesystem::path p(path);
    return std::filesystem::is_regular_file(p, ec);
}

bool read_payload_file(const char* path, std::vector<unsigned char>& buffer)
{
    std::ifstream stream(path, std::ios::binary | std::ios::ate);
    if (!stream)
    {
        return false;
    }

    std::streamsize size = stream.tellg();
    if (size <= 0)
    {
        return false;
    }
    buffer.resize(static_cast<size_t>(size));
    stream.seekg(0, std::ios::beg);
    return static_cast<bool>(stream.read(reinterpret_cast<char*>(buffer.data()), size));
}
