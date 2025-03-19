#pragma once

#include "pch.h"
#include <filesystem>
#include <iostream>
#include <string>
#include <map>

using namespace Microsoft::WRL;

namespace newplus
{
    class template_item
    {
    public:
        template_item(const std::filesystem::path entry);

        std::wstring get_menu_title(const bool show_extension, const bool show_starting_digits, const bool show_resolved_variables) const;

        std::wstring get_target_filename(const bool include_starting_digits) const;

        std::wstring get_explorer_icon() const;
        
        HICON get_explorer_icon_handle() const;

        std::filesystem::path copy_object_to(const HWND window_handle, const std::filesystem::path destination) const;

        void refresh_target(const std::filesystem::path target_final_fullpath) const;

        void enter_rename_mode(const std::filesystem::path target_fullpath) const;

        std::filesystem::path path;

    private:
        static void rename_on_other_thread_workaround(const std::filesystem::path target_fullpath);

        std::wstring remove_starting_digits_from_filename(std::wstring filename) const;
    };
}