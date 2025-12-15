#pragma once

#include <vector>

// Checks for existence of a non-directory file.
bool payload_exists(const char* path);

// Reads a payload file into a buffer. Returns false on failure.
bool read_payload_file(const char* path, std::vector<unsigned char>& buffer);
