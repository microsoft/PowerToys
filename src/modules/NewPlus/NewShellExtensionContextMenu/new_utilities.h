#pragma once

#include "pch.h"

#include <common/utils/process_path.h>
#include <common/utils/package.h>

#include "constants.h"
#include "settings.h"

#pragma comment(lib, "Shlwapi.lib")

namespace newplus::utilities
{

    inline std::wstring get_explorer_icon(std::filesystem::path path)
    {
        SHFILEINFO shell_file_info = { 0 };
        const std::wstring filepath = path.wstring();
        DWORD_PTR result = SHGetFileInfo(filepath.c_str(), 0, &shell_file_info, sizeof(shell_file_info), SHGFI_ICONLOCATION);
        std::wstring icon_path = shell_file_info.szDisplayName;
        if (icon_path != L"")
        {
            const int icon_index = shell_file_info.iIcon;
            std::wstring icon_resource = icon_path + std::wstring(L",") + std::to_wstring(icon_index);
            return icon_resource;
        }

        WCHAR icon_resource_specifier[MAX_PATH] = { 0 };
        DWORD buffer_length = MAX_PATH;
        const std::wstring extension = path.extension().wstring();
        const HRESULT hr = AssocQueryString(ASSOCF_INIT_IGNOREUNKNOWN,
                                            ASSOCSTR_DEFAULTICON,
                                            extension.c_str(),
                                            NULL,
                                            icon_resource_specifier,
                                            &buffer_length);
        const std::wstring icon_resource = icon_resource_specifier;
        return icon_resource;
    }

    inline bool is_hidden(const std::filesystem::path path)
    {
        const std::filesystem::path::string_type name = path.filename();
        if (name == constants::non_localizable::desktop_ini_filename)
        {
            return true;
        }

        return false;
    }

    inline bool wstring_same_when_comparing_ignore_case(std::wstring stringA, std::wstring stringB)
    {
        transform(stringA.begin(), stringA.end(), stringA.begin(), towupper);
        transform(stringB.begin(), stringB.end(), stringB.begin(), towupper);

        return (stringA == stringB);
    }

    inline void process_pending_window_messages(HWND window_handle = NULL)
    {
        if (window_handle == NULL)
        {
            window_handle = GetActiveWindow();
        }

        MSG current_message;
        while (PeekMessage(&current_message, window_handle, NULL, NULL, PM_REMOVE))
        {
            DispatchMessage(&current_message);
        }
    }

    inline std::wstring get_new_template_folder_location()
    {
        return NewSettingsInstance().GetTemplateLocation();
    }

    inline bool get_newplus_setting_hide_extension()
    {
        return NewSettingsInstance().GetHideFileExtension();
    }

    inline bool get_newplus_setting_hide_starting_digits()
    {
        return NewSettingsInstance().GetHideStartingDigits();
    }

    inline void create_folder_if_not_exist(const std::filesystem::path path)
    {
        std::filesystem::create_directory(path);
    }

    inline std::wstring get_new_icon_resource_filepath(const HMODULE module_instance_handle, const Theme theme)
    {
        auto iconResourcePath = get_module_folderpath(module_instance_handle);

        if (theme == Theme::Dark)
        {
            iconResourcePath += constants::non_localizable::new_icon_dark_resource_relative_path;
        }
        else
        {
            // Defaulting to the Light icon
            iconResourcePath += constants::non_localizable::new_icon_light_resource_relative_path;
        }

        return iconResourcePath;
    }

    inline std::wstring get_open_templates_icon_resource_filepath(const HMODULE module_instance_handle, const Theme theme)
    {
        auto iconResourcePath = get_module_folderpath(module_instance_handle);

        if (theme == Theme::Dark)
        {
            iconResourcePath += constants::non_localizable::open_templates_icon_dark_resource_relative_path;
        }
        else
        {
            // Defaulting to the Light icon
            iconResourcePath += constants::non_localizable::open_templates_icon_light_resource_relative_path;
        }

        return iconResourcePath;
    }

    inline void init_logger()
    {
        LoggerHelpers::init_logger(
            constants::non_localizable::powertoy_name,
            constants::non_localizable::module_name,
            LogSettings::newLoggerName);
    }

    inline void register_msix_package()
    {
        if (package::IsWin11OrGreater())
        {
            static const auto new_dll_path = get_module_folderpath(module_instance_handle);
            auto new_package_uri = new_dll_path + L"\\" + constants::non_localizable::msix_package_name;

            if (!package::IsPackageRegistered(constants::non_localizable::context_menu_package_name))
            {
                package::RegisterSparsePackage(new_dll_path, new_package_uri);
            }
        }
    }

    inline std::wstring get_path_from_unknown_site(const ComPtr<IUnknown> site_of_folder)
    {
        ComPtr<IServiceProvider> service_provider;
        site_of_folder->QueryInterface(IID_PPV_ARGS(&service_provider));
        ComPtr<IFolderView> folder_view;
        service_provider->QueryService(__uuidof(IFolderView), IID_PPV_ARGS(&folder_view));
        ComPtr<IShellFolder> shell_folder;
        folder_view->GetFolder(IID_PPV_ARGS(&shell_folder));
        STRRET strings_returned;
        shell_folder->GetDisplayNameOf(0, SHGDN_FORPARSING, &strings_returned);
        LPWSTR path;
        StrRetToStr(&strings_returned, NULL, &path);
        return path;
    }

    inline std::wstring get_path_from_folder_view(const ComPtr<IFolderView> folder_view)
    {
        ComPtr<IShellFolder> shell_folder;
        folder_view->GetFolder(IID_PPV_ARGS(&shell_folder));
        STRRET strings_returned;
        shell_folder->GetDisplayNameOf(0, SHGDN_FORPARSING, &strings_returned);
        LPWSTR path;
        StrRetToStr(&strings_returned, NULL, &path);
        return path;
    }

}
