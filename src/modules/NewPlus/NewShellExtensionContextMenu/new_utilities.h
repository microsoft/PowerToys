#pragma once

#include "pch.h"

#include <common/utils/process_path.h>
#include <common/utils/package.h>

#include "constants.h"
#include "settings.h"
#include "template_item.h"
#include "trace.h"
#include "helpers_variables.h"

#pragma comment(lib, "Shlwapi.lib")

using namespace newplus;

namespace newplus::utilities
{
    size_t get_saved_number_of_templates();
    void set_saved_number_of_templates(size_t templates);

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

    inline HICON get_explorer_icon_handle(std::filesystem::path path)
    {
        SHFILEINFO shell_file_info = { 0 };
        const std::wstring filepath = path.wstring();
        DWORD_PTR result = SHGetFileInfo(filepath.c_str(), 0, &shell_file_info, sizeof(shell_file_info), SHGFI_ICON);
        if (shell_file_info.hIcon)
        {
            return shell_file_info.hIcon;
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
        
        const auto icon_x = GetSystemMetrics(SM_CXSMICON);
        const auto icon_y = GetSystemMetrics(SM_CYSMICON);
        HICON hIcon = static_cast<HICON>(LoadImage(NULL, icon_resource.c_str(), IMAGE_ICON, icon_x, icon_y, LR_LOADFROMFILE));
        return hIcon;
    }

    inline bool wstring_same_when_comparing_ignore_case(std::wstring stringA, std::wstring stringB)
    {
        transform(stringA.begin(), stringA.end(), stringA.begin(), towupper);
        transform(stringB.begin(), stringB.end(), stringB.begin(), towupper);

        return (stringA == stringB);
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

    inline bool get_newplus_setting_resolve_variables()
    {
        return NewSettingsInstance().GetReplaceVariables();
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

    inline bool is_desktop_folder(const std::filesystem::path target_fullpath)
    {
        TCHAR desktop_path[MAX_PATH];
        if (SUCCEEDED(SHGetFolderPath(NULL, CSIDL_DESKTOP, NULL, 0, desktop_path)))
        {
            return StrCmpIW(target_fullpath.c_str(), desktop_path) == 0;
        }
        return false;
    }

    inline void explorer_enter_rename_mode(const std::filesystem::path target_fullpath_of_new_instance)
    {
        const std::filesystem::path path_without_new_file_or_dir = target_fullpath_of_new_instance.parent_path();
        const std::filesystem::path new_file_or_dir_without_path = target_fullpath_of_new_instance.filename();

        ComPtr<IShellWindows> shell_windows;

        HRESULT hr;
        if (FAILED(CoCreateInstance(CLSID_ShellWindows, NULL, CLSCTX_ALL, IID_PPV_ARGS(&shell_windows))))
        {
            return;
        }

        long window_handle;
        ComPtr<IDispatch> shell_window;
        const bool object_created_on_desktop = is_desktop_folder(path_without_new_file_or_dir.c_str());
        if (object_created_on_desktop)
        {
            // Special handling for desktop folder
            VARIANT empty_yet_needed_incl_init;
            VariantInit(&empty_yet_needed_incl_init);

            if (FAILED(shell_windows->FindWindowSW(&empty_yet_needed_incl_init, &empty_yet_needed_incl_init, SWC_DESKTOP, &window_handle, SWFO_NEEDDISPATCH, &shell_window)))
            {
                return;
            }
        }
        else
        {
            long count_of_shell_windows = 0;
            shell_windows->get_Count(&count_of_shell_windows);

            for (long i = 0; i < count_of_shell_windows; ++i)
            {
                ComPtr<IWebBrowserApp> web_browser_app;
                VARIANT v;
                VariantInit(&v);
                V_VT(&v) = VT_I4;
                V_I4(&v) = i;
                hr = shell_windows->Item(v, &shell_window);
                if (SUCCEEDED(hr) && shell_window)
                {
                    hr = shell_window.As(&web_browser_app);
                    if (SUCCEEDED(hr))
                    {
                        BSTR folder_view_location;
                        hr = web_browser_app->get_LocationURL(&folder_view_location);
                        if (SUCCEEDED(hr) && folder_view_location)
                        {
                            wchar_t path[MAX_PATH];
                            DWORD pathLength = ARRAYSIZE(path);
                            hr = PathCreateFromUrl(folder_view_location, path, &pathLength, 0);
                            SysFreeString(folder_view_location);
                            if (SUCCEEDED(hr) && StrCmpIW(path_without_new_file_or_dir.c_str(), path) == 0)
                            {
                                break;
                            }
                        }
                    }
                }
                shell_window = nullptr;
            }
        }

        if (!shell_window)
        {
            return;
        }

        ComPtr<IServiceProvider> service_provider;
        shell_window.As(&service_provider);
        ComPtr<IShellBrowser> shell_browser;
        service_provider->QueryService(SID_STopLevelBrowser, IID_PPV_ARGS(&shell_browser));
        ComPtr<IShellView> shell_view;
        shell_browser->QueryActiveShellView(&shell_view);
        ComPtr<IFolderView> folder_view;
        shell_view.As(&folder_view);

        // Find the newly created object (file or folder)
        // And put object into edit mode (SVSI_EDIT) and if desktop also reposition
        int number_of_objects_in_view = 0;
        bool done = false;
        folder_view->ItemCount(SVGIO_ALLVIEW, &number_of_objects_in_view);
        for (int i = 0; i < number_of_objects_in_view && !done; ++i)
        {
            std::wstring path_of_item(MAX_PATH, 0);
            LPITEMIDLIST shell_item_ids;

            folder_view->Item(i, &shell_item_ids);
            SHGetPathFromIDList(shell_item_ids, &path_of_item[0]);

            const std::wstring current_filename = std::filesystem::path(path_of_item.c_str()).filename();

            if (utilities::wstring_same_when_comparing_ignore_case(new_file_or_dir_without_path, current_filename))
            {
                const DWORD common_select_flags = SVSI_EDIT | SVSI_SELECT | SVSI_DESELECTOTHERS | SVSI_ENSUREVISIBLE | SVSI_FOCUSED;

                if (object_created_on_desktop)
                {
                    // Newly created object is on the desktop -- reposition under mouse and enter rename mode
                    LPCITEMIDLIST shell_item_to_select_and_position[] = { shell_item_ids };
                    POINT mouse_position;
                    GetCursorPos(&mouse_position);
                    mouse_position.x -= GetSystemMetrics(SM_CXMENUSIZE);
                    mouse_position.x = max(mouse_position.x, 20);
                    mouse_position.y -= GetSystemMetrics(SM_CXMENUSIZE)/2;
                    mouse_position.y = max(mouse_position.y, 20);
                        POINT position[] = { mouse_position };
                    folder_view->SelectAndPositionItems(1, shell_item_to_select_and_position, position, common_select_flags | SVSI_POSITIONITEM);
                }
                else
                {
                    // Enter rename mode
                    folder_view->SelectItem(i, common_select_flags);
                }
                done = true;
            }
            CoTaskMemFree(shell_item_ids);
        }
    }

    inline void update_last_write_time(const std::filesystem::path path)
    {
        const std::filesystem::file_time_type now = std::filesystem::file_time_type::clock::now();

        std::filesystem::last_write_time(path, now);
        
        if (std::filesystem::is_directory(path))
        {
            for (const auto& entry : std::filesystem::recursive_directory_iterator(path))
            {
                std::filesystem::last_write_time(entry.path(), now);
            }
        }
    }

    inline HRESULT copy_template(const template_item* template_entry, const ComPtr<IUnknown> site_of_folder)
    {
        HRESULT hr = S_OK;

        try
        {
            Logger::info(L"Copying template");

            if (newplus::utilities::get_saved_number_of_templates() >= 0)
            {
                // Log that context menu was shown and with how many items
                trace.UpdateState(true);
                Trace::EventShowTemplateItems(newplus::utilities::get_saved_number_of_templates());
                trace.Flush();
                trace.UpdateState(false);
            }

            // Determine target path of where context menu was displayed
            const auto target_path_name = utilities::get_path_from_unknown_site(site_of_folder);

            // Determine initial filename
            std::filesystem::path source_fullpath = template_entry->path;
            std::filesystem::path target_fullpath = std::wstring(target_path_name);

            // Get target name without starting digits as appropriate
            const std::wstring target_name = template_entry->get_target_filename(!utilities::get_newplus_setting_hide_starting_digits());

            // Get initial resolved name
            target_fullpath /= target_name;

            // Expand variables in name of the target path
            if (utilities::get_newplus_setting_resolve_variables())
            {
                target_fullpath = helpers::variables::resolve_variables_in_path(target_fullpath);
            }

            // See if our target already exist, and if so then generate a unique name
            target_fullpath = helpers::filesystem::make_unique_path_name(target_fullpath);

            // Finally copy file/folder/subfolders
            std::filesystem::path target_final_fullpath = template_entry->copy_object_to(GetActiveWindow(), target_fullpath);

            // Resolve variables and rename files in newly copied folders and subfolders and files
            if (utilities::get_newplus_setting_resolve_variables() && helpers::filesystem::is_directory(target_final_fullpath))
            {
                helpers::variables::resolve_variables_in_filename_and_rename_files(target_final_fullpath);
            }

            // Touch all files and set last modified to "now"
            update_last_write_time(target_final_fullpath);

            // Consider copy completed. If we do tracing after enter_rename_mode, then rename mode won't consistently work
            trace.UpdateState(true);
            Trace::EventCopyTemplate(target_final_fullpath.extension().c_str());
            Trace::EventCopyTemplateResult(hr);
            trace.Flush();
            trace.UpdateState(false);

            // Refresh folder items
            template_entry->refresh_target(target_final_fullpath);

            // Enter rename mode
            template_entry->enter_rename_mode(target_final_fullpath);
        }
        catch (const std::exception& ex)
        {
            Logger::error(ex.what());

            hr = S_FALSE;
            trace.UpdateState(true);
            Trace::EventCopyTemplateResult(hr);
            trace.Flush();
            trace.UpdateState(false);
        }

        return hr;
    }

    inline HRESULT open_template_folder(const std::filesystem::path template_folder)
    {
        HRESULT hr = S_OK;

        try
        {
            Logger::info(L"Open templates folder");

            if (newplus::utilities::get_saved_number_of_templates() >= 0)
            {
                // Log that context menu was shown and with how many items
                trace.UpdateState(true);
                Trace::EventShowTemplateItems(newplus::utilities::get_saved_number_of_templates());
                trace.Flush();
                trace.UpdateState(false);
            }

            const std::wstring verb_hardcoded_do_not_change = L"open";
            ShellExecute(nullptr, verb_hardcoded_do_not_change.c_str(), template_folder.c_str(), NULL, NULL, SW_SHOWNORMAL);

            trace.UpdateState(true);
            Trace::EventOpenTemplates();
            trace.Flush();
            trace.UpdateState(false);
        }
        catch (const std::exception& ex)
        {
            Logger::error(ex.what());

            hr = S_FALSE;
        }

        return hr;
    }
}
