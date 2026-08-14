#pragma once

#include <filesystem>
#include <string>
#include <windows.h>

namespace newplus::icon_utilities
{
    // is_directory=true skips the per-file icon cache (directory icons can change via desktop.ini)
    std::wstring get_explorer_icon(const std::filesystem::path& path, bool is_directory = false);
    HICON get_explorer_icon_handle(const std::filesystem::path& path);
}
