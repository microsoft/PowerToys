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

        std::wstring get_menu_title(const bool show_extention) const;
        std::wstring get_explorer_icon() const;

        std::filesystem::path copy_object_to(const HWND window_handle, const std::filesystem::path destination) const;
        void enter_rename_mode(const ComPtr<IUnknown> site, const std::filesystem::path target_folder) const;

        std::filesystem::path path;

    private:
        static void rename_on_other_thread_workaround(const ComPtr<IUnknown> site, const std::filesystem::path target_fullpath);
    };
}