#include "pe_utils.h"

#include <algorithm>
#include <cstdint>

#ifdef _WIN32

bool parse_pe_image(const std::vector<unsigned char>& image, pe_image& info, dispatch_outcome& failure, bool require_executable)
{
    info = {};
    if (image.size() < sizeof(IMAGE_DOS_HEADER))
    {
        failure = { false, "PAYLOAD_INVALID", "Payload is not a valid PE image." };
        return false;
    }

    const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(image.data());
    if (dos->e_magic != IMAGE_DOS_SIGNATURE || dos->e_lfanew <= 0)
    {
        failure = { false, "PAYLOAD_INVALID", "Payload is not a valid PE image." };
        return false;
    }

    size_t nt_offset = static_cast<size_t>(dos->e_lfanew);
    if (nt_offset + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER) > image.size())
    {
        failure = { false, "PAYLOAD_INVALID", "Payload PE headers are incomplete." };
        return false;
    }

    const auto signature = *reinterpret_cast<const DWORD*>(image.data() + nt_offset);
    if (signature != IMAGE_NT_SIGNATURE)
    {
        failure = { false, "PAYLOAD_INVALID", "Payload is missing PE signature." };
        return false;
    }

    const auto* file_header = reinterpret_cast<const IMAGE_FILE_HEADER*>(image.data() + nt_offset + sizeof(DWORD));
    if (file_header->NumberOfSections == 0)
    {
        failure = { false, "PAYLOAD_INVALID", "Payload section table is invalid." };
        return false;
    }

    const bool is_dll = (file_header->Characteristics & IMAGE_FILE_DLL) != 0;
    const bool is_exe = (file_header->Characteristics & IMAGE_FILE_EXECUTABLE_IMAGE) != 0;
    if (require_executable && (!is_exe || is_dll))
    {
        failure = { false, "PAYLOAD_INVALID", "Payload must be an executable image." };
        return false;
    }

    size_t optional_offset = nt_offset + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER);
    if (file_header->SizeOfOptionalHeader < sizeof(WORD) ||
        optional_offset + file_header->SizeOfOptionalHeader > image.size())
    {
        failure = { false, "PAYLOAD_INVALID", "Payload PE optional header is incomplete." };
        return false;
    }

    WORD magic = *reinterpret_cast<const WORD*>(image.data() + optional_offset);
    const IMAGE_DATA_DIRECTORY* data_directories = nullptr;
    if (magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC)
    {
        const auto* opt = reinterpret_cast<const IMAGE_OPTIONAL_HEADER32*>(image.data() + optional_offset);
        info.is64 = false;
        info.image_base = opt->ImageBase;
        info.size_of_image = opt->SizeOfImage;
        info.size_of_headers = opt->SizeOfHeaders;
        info.entry_rva = opt->AddressOfEntryPoint;
        info.reloc_rva = opt->DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC].VirtualAddress;
        info.reloc_size = opt->DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC].Size;
        data_directories = opt->DataDirectory;
    }
    else if (magic == IMAGE_NT_OPTIONAL_HDR64_MAGIC)
    {
        const auto* opt = reinterpret_cast<const IMAGE_OPTIONAL_HEADER64*>(image.data() + optional_offset);
        info.is64 = true;
        info.image_base = opt->ImageBase;
        info.size_of_image = opt->SizeOfImage;
        info.size_of_headers = opt->SizeOfHeaders;
        info.entry_rva = opt->AddressOfEntryPoint;
        info.reloc_rva = opt->DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC].VirtualAddress;
        info.reloc_size = opt->DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC].Size;
        data_directories = opt->DataDirectory;
    }
    else
    {
        failure = { false, "PAYLOAD_INVALID", "Payload PE optional header is not recognized." };
        return false;
    }

    size_t section_offset = optional_offset + file_header->SizeOfOptionalHeader;
    size_t section_count = file_header->NumberOfSections;
    size_t section_bytes = section_count * sizeof(IMAGE_SECTION_HEADER);
    if (section_offset + section_bytes > image.size())
    {
        failure = { false, "PAYLOAD_INVALID", "Payload section headers are incomplete." };
        return false;
    }

    if (info.size_of_headers == 0 || info.size_of_image == 0)
    {
        failure = { false, "PAYLOAD_INVALID", "Payload size fields are invalid." };
        return false;
    }

    if (info.size_of_headers > image.size())
    {
        failure = { false, "PAYLOAD_INVALID", "Payload headers exceed file size." };
        return false;
    }

    info.is_dll = is_dll;
    info.optional_offset = optional_offset;
    info.base = image.data();
    info.size = image.size();
    info.sections = reinterpret_cast<const IMAGE_SECTION_HEADER*>(image.data() + section_offset);
    info.section_count = section_count;
    info.data_directories = data_directories;
    return true;
}

bool rva_to_offset(const pe_image& info, const std::vector<unsigned char>& image, uint32_t rva, uint32_t& out_offset)
{
    if (rva < info.size_of_headers)
    {
        out_offset = rva;
        return out_offset < image.size();
    }

    for (size_t i = 0; i < info.section_count; ++i)
    {
        const IMAGE_SECTION_HEADER& section = info.sections[i];
        uint32_t size = std::max(section.Misc.VirtualSize, section.SizeOfRawData);
        if (rva >= section.VirtualAddress && rva < section.VirtualAddress + size)
        {
            out_offset = section.PointerToRawData + (rva - section.VirtualAddress);
            return out_offset < image.size();
        }
    }

    return false;
}

bool find_export_offset(const std::vector<unsigned char>& image, const std::string& export_name, uint32_t& out_offset)
{
    dispatch_outcome failure{};
    pe_image info{};
    if (!parse_pe_image(image, info, failure, false))
    {
        return false;
    }

    if (!info.data_directories)
    {
        return false;
    }

    const IMAGE_DATA_DIRECTORY& export_dir = info.data_directories[IMAGE_DIRECTORY_ENTRY_EXPORT];
    if (export_dir.VirtualAddress == 0 || export_dir.Size == 0)
    {
        return false;
    }

    uint32_t export_offset = 0;
    if (!rva_to_offset(info, image, export_dir.VirtualAddress, export_offset))
    {
        return false;
    }

    if (export_offset + sizeof(IMAGE_EXPORT_DIRECTORY) > info.size)
    {
        return false;
    }

    const auto* export_table = reinterpret_cast<const IMAGE_EXPORT_DIRECTORY*>(info.base + export_offset);

    uint32_t names_offset = 0;
    uint32_t ordinals_offset = 0;
    uint32_t functions_offset = 0;
    if (!rva_to_offset(info, image, export_table->AddressOfNames, names_offset) ||
        !rva_to_offset(info, image, export_table->AddressOfNameOrdinals, ordinals_offset) ||
        !rva_to_offset(info, image, export_table->AddressOfFunctions, functions_offset))
    {
        return false;
    }

    if (names_offset >= info.size || ordinals_offset >= info.size || functions_offset >= info.size)
    {
        return false;
    }

    const auto* name_rvas = reinterpret_cast<const uint32_t*>(info.base + names_offset);
    const auto* ordinals = reinterpret_cast<const uint16_t*>(info.base + ordinals_offset);
    const auto* functions = reinterpret_cast<const uint32_t*>(info.base + functions_offset);

    for (uint32_t i = 0; i < export_table->NumberOfNames; ++i)
    {
        uint32_t name_rva = name_rvas[i];
        uint32_t name_offset = 0;
        if (!rva_to_offset(info, image, name_rva, name_offset))
        {
            continue;
        }

        if (name_offset >= info.size)
        {
            continue;
        }

        const char* name_ptr = reinterpret_cast<const char*>(info.base + name_offset);
        if (export_name == name_ptr)
        {
            uint16_t ordinal_index = ordinals[i];
            if (ordinal_index >= export_table->NumberOfFunctions)
            {
                return false;
            }

            uint32_t function_rva = functions[ordinal_index];
            uint32_t function_offset = 0;
            if (!rva_to_offset(info, image, function_rva, function_offset))
            {
                return false;
            }

            out_offset = function_offset;
            return out_offset < info.size;
        }
    }

    return false;
}

bool apply_relocations(std::vector<unsigned char>& image, pe_image& info, uint64_t new_base, dispatch_outcome& failure)
{
    if (new_base == info.image_base)
    {
        return true;
    }

    if (info.reloc_rva == 0 || info.reloc_size == 0)
    {
        failure = { false, "IMAGE_BASE_UNAVAILABLE", "Payload cannot be relocated to a new base address." };
        return false;
    }

    uint32_t reloc_offset = 0;
    if (!rva_to_offset(info, image, info.reloc_rva, reloc_offset))
    {
        failure = { false, "RELOCATION_FAILED", "Relocation directory could not be resolved." };
        return false;
    }

    uint64_t delta = new_base - info.image_base;
    size_t cursor = reloc_offset;
    size_t remaining = info.reloc_size;

    while (remaining >= sizeof(IMAGE_BASE_RELOCATION))
    {
        if (cursor + sizeof(IMAGE_BASE_RELOCATION) > image.size())
        {
            failure = { false, "RELOCATION_FAILED", "Relocation directory is out of bounds." };
            return false;
        }

        const auto* block = reinterpret_cast<const IMAGE_BASE_RELOCATION*>(image.data() + cursor);
        if (block->SizeOfBlock < sizeof(IMAGE_BASE_RELOCATION) || block->SizeOfBlock > remaining)
        {
            failure = { false, "RELOCATION_FAILED", "Relocation block size is invalid." };
            return false;
        }

        size_t entry_count = (block->SizeOfBlock - sizeof(IMAGE_BASE_RELOCATION)) / sizeof(WORD);
        const auto* entries = reinterpret_cast<const WORD*>(image.data() + cursor + sizeof(IMAGE_BASE_RELOCATION));

        for (size_t i = 0; i < entry_count; ++i)
        {
            WORD entry = entries[i];
            WORD type = entry >> 12;
            WORD offset = entry & 0x0FFF;
            if (type == IMAGE_REL_BASED_ABSOLUTE)
            {
                continue;
            }

            uint32_t patch_rva = block->VirtualAddress + offset;
            uint32_t patch_offset = 0;
            if (!rva_to_offset(info, image, patch_rva, patch_offset))
            {
                failure = { false, "RELOCATION_FAILED", "Relocation entry points outside image." };
                return false;
            }

            if (type == IMAGE_REL_BASED_HIGHLOW && !info.is64)
            {
                if (patch_offset + sizeof(uint32_t) > image.size())
                {
                    failure = { false, "RELOCATION_FAILED", "Relocation entry exceeds image bounds." };
                    return false;
                }
                auto* value = reinterpret_cast<uint32_t*>(image.data() + patch_offset);
                *value += static_cast<uint32_t>(delta);
            }
            else if (type == IMAGE_REL_BASED_DIR64 && info.is64)
            {
                if (patch_offset + sizeof(uint64_t) > image.size())
                {
                    failure = { false, "RELOCATION_FAILED", "Relocation entry exceeds image bounds." };
                    return false;
                }
                auto* value = reinterpret_cast<uint64_t*>(image.data() + patch_offset);
                *value += delta;
            }
        }

        cursor += block->SizeOfBlock;
        remaining -= block->SizeOfBlock;

        if (block->SizeOfBlock == 0)
        {
            break;
        }
    }

    if (info.is64)
    {
        auto* opt64 = reinterpret_cast<IMAGE_OPTIONAL_HEADER64*>(image.data() + info.optional_offset);
        opt64->ImageBase = new_base;
    }
    else
    {
        auto* opt32 = reinterpret_cast<IMAGE_OPTIONAL_HEADER32*>(image.data() + info.optional_offset);
        opt32->ImageBase = static_cast<DWORD>(new_base);
    }

    info.image_base = new_base;
    return true;
}

bool write_image_to_process(HANDLE process,
    const pe_image& info,
    const std::vector<unsigned char>& image,
    void* remote_base,
    dispatch_outcome& failure)
{
    if (info.size_of_headers > image.size())
    {
        failure = { false, "PAYLOAD_INVALID", "Payload headers exceed file size." };
        return false;
    }

    if (!::WriteProcessMemory(process, remote_base, image.data(), info.size_of_headers, nullptr))
    {
        failure = { false, "PAYLOAD_WRITE_FAILED", "Failed to write payload headers." };
        return false;
    }

    for (size_t i = 0; i < info.section_count; ++i)
    {
        const IMAGE_SECTION_HEADER& section = info.sections[i];
        if (section.SizeOfRawData == 0)
        {
            continue;
        }

        size_t raw_end = static_cast<size_t>(section.PointerToRawData) + section.SizeOfRawData;
        if (raw_end > image.size())
        {
            failure = { false, "PAYLOAD_INVALID", "Payload section data exceeds file size." };
            return false;
        }

        const void* src = image.data() + section.PointerToRawData;
        void* dest = static_cast<unsigned char*>(remote_base) + section.VirtualAddress;
        if (!::WriteProcessMemory(process, dest, src, section.SizeOfRawData, nullptr))
        {
            failure = { false, "PAYLOAD_WRITE_FAILED", "Failed to write payload section data." };
            return false;
        }
    }

    return true;
}

#else

bool parse_pe_image(const std::vector<unsigned char>&, pe_image&, dispatch_outcome& failure, bool)
{
    failure = { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Native injector is not implemented on this platform." };
    return false;
}

bool rva_to_offset(const pe_image&, const std::vector<unsigned char>&, uint32_t, uint32_t&)
{
    return false;
}

bool find_export_offset(const std::vector<unsigned char>&, const std::string&, uint32_t&)
{
    return false;
}

bool apply_relocations(std::vector<unsigned char>&, pe_image&, uint64_t, dispatch_outcome& failure)
{
    failure = { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Native injector is not implemented on this platform." };
    return false;
}

bool write_image_to_process(HANDLE, const pe_image&, const std::vector<unsigned char>&, void*, dispatch_outcome& failure)
{
    failure = { false, "TECHNIQUE_UNSUPPORTED_PLATFORM", "Native injector is not implemented on this platform." };
    return false;
}

#endif
