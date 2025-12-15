#include "payload_utils.h"

#include <fstream>
#include <vector>
#include <windows.h>

bool payload_exists(const char* path)
{
    if (!path || path[0] == '\0')
    {
        return false;
    }
    const DWORD attributes = ::GetFileAttributesA(path);
    return attributes != INVALID_FILE_ATTRIBUTES && !(attributes & FILE_ATTRIBUTE_DIRECTORY);
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
