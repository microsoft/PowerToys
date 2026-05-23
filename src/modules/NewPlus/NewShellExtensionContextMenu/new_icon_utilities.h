#pragma once

#include <filesystem>
#include <string>
#include <windows.h>

namespace newplus::utilities
{
    // is_directory=true skips the extension cache (directory icons can change via desktop.ini)
    std::wstring get_explorer_icon(std::filesystem::path path, bool is_directory = false);
    HICON get_explorer_icon_handle(std::filesystem::path path);
}
