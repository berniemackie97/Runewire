#include "payload_utils.h"

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
