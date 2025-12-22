#pragma once

#include "technique_dispatch.h"

#include <cstdint>
#include <string>
#include <vector>

#ifdef _WIN32
#include <windows.h>
#else
using HANDLE = void*;
struct IMAGE_SECTION_HEADER {};
struct IMAGE_DATA_DIRECTORY {};
#endif

struct pe_image
{
    bool is64;
    bool is_dll;
    uint64_t image_base;
    uint32_t size_of_image;
    uint32_t size_of_headers;
    uint32_t entry_rva;
    uint32_t reloc_rva;
    uint32_t reloc_size;
    size_t optional_offset;
    const unsigned char* base;
    size_t size;
    const IMAGE_SECTION_HEADER* sections;
    size_t section_count;
    const IMAGE_DATA_DIRECTORY* data_directories;
};

bool parse_pe_image(const std::vector<unsigned char>& image, pe_image& info, dispatch_outcome& failure, bool require_executable);
bool rva_to_offset(const pe_image& info, const std::vector<unsigned char>& image, uint32_t rva, uint32_t& out_offset);
bool find_export_offset(const std::vector<unsigned char>& image, const std::string& export_name, uint32_t& out_offset);
bool apply_relocations(std::vector<unsigned char>& image, pe_image& info, uint64_t new_base, dispatch_outcome& failure);
bool write_image_to_process(HANDLE process, const pe_image& info, const std::vector<unsigned char>& image, void* remote_base, dispatch_outcome& failure);
