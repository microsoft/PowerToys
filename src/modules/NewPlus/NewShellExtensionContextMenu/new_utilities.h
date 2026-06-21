#pragma once

#include "pch.h"

#include <common/utils/process_path.h>
#include <common/utils/package.h>

#include "constants.h"
#include "settings.h"
#include "template_item.h"
#include "trace.h"
#include "helpers_variables.h"
#include <shellscalingapi.h>

#pragma comment(lib, "Shcore.lib")
#pragma comment(lib, "Shlwapi.lib")

using namespace newplus;

namespace newplus::utilities
{
    size_t get_saved_number_of_templates();
    void set_saved_number_of_templates(size_t templates);

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

            if (!package::IsPackageRegisteredWithPowerToysVersion(constants::non_localizable::context_menu_package_name))
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

    inline bool explorer_enter_rename_mode_and_reposition(const std::filesystem::path target_fullpath_of_new_instance, const POINT mouse_position_at_time_of_invoke, const bool enter_rename_mode = true)
    {
        const std::filesystem::path path_without_new_file_or_dir = target_fullpath_of_new_instance.parent_path();
        const std::filesystem::path new_file_or_dir_without_path = target_fullpath_of_new_instance.filename();

        CComPtr<IShellWindows> shell_windows;

        HRESULT hr;
        if (FAILED(CoCreateInstance(CLSID_ShellWindows, NULL, CLSCTX_ALL, IID_PPV_ARGS(&shell_windows))))
        {
            return false;
        }

        long desktop_window_handle = 0;
        CComPtr<IDispatch> shell_window;
        const bool object_created_on_desktop = is_desktop_folder(path_without_new_file_or_dir.c_str());
        if (object_created_on_desktop)
        {
            // Special handling for desktop folder
            VARIANT empty_yet_needed_incl_init;
            VariantInit(&empty_yet_needed_incl_init);

            if (FAILED(shell_windows->FindWindowSW(&empty_yet_needed_incl_init, &empty_yet_needed_incl_init, SWC_DESKTOP, &desktop_window_handle, SWFO_NEEDDISPATCH, &shell_window)))
            {
                return false;
            }
        }
        else
        {
            long count_of_shell_windows = 0;
            shell_windows->get_Count(&count_of_shell_windows);

            for (long i = 0; i < count_of_shell_windows; ++i)
            {
                CComPtr<IWebBrowserApp> web_browser_app;
                VARIANT v;
                VariantInit(&v);
                V_VT(&v) = VT_I4;
                V_I4(&v) = i;
                hr = shell_windows->Item(v, &shell_window);
                if (SUCCEEDED(hr) && shell_window)
                {
                    hr = shell_window->QueryInterface(&web_browser_app);
                    if (SUCCEEDED(hr))
                    {
                        BSTR folder_view_location;
                        hr = web_browser_app->get_LocationURL(&folder_view_location);
                        if (SUCCEEDED(hr) && folder_view_location)
                        {
                            wchar_t path[MAX_PATH * 2];
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
            return false;
        }

        CComPtr<IServiceProvider> service_provider;
        shell_window->QueryInterface(&service_provider);
        CComPtr<IShellBrowser> shell_browser;
        service_provider->QueryService(SID_STopLevelBrowser, IID_PPV_ARGS(&shell_browser));
        CComPtr<IShellView> shell_view;
        shell_browser->QueryActiveShellView(&shell_view);
        CComPtr<IFolderView> folder_view;
        shell_view->QueryInterface(&folder_view);

        // Find the newly created object (file or folder)
        // And put object into edit mode (SVSI_EDIT) and if desktop also reposition
        int number_of_objects_in_view = 0;
        bool done = false;
        folder_view->ItemCount(SVGIO_ALLVIEW, &number_of_objects_in_view);
        for (int i = 0; i < number_of_objects_in_view && !done; ++i)
        {
            PITEMID_CHILD shell_item_id = nullptr;

            folder_view->Item(i, &shell_item_id);

            wchar_t path_buffer[MAX_PATH * 2] = { 0 };
            if (!SHGetPathFromIDListW(reinterpret_cast<PCIDLIST_ABSOLUTE>(shell_item_id), path_buffer))
            {
                CoTaskMemFree(shell_item_id);
                continue;
            }

            const std::wstring current_filename = std::filesystem::path(path_buffer).filename();

            if (newplus::utilities::wstring_same_when_comparing_ignore_case(new_file_or_dir_without_path, current_filename))
            {
                const DWORD common_select_flags = (enter_rename_mode ? SVSI_EDIT : 0) | SVSI_SELECT | SVSI_DESELECTOTHERS | SVSI_ENSUREVISIBLE | SVSI_FOCUSED;

                if (object_created_on_desktop)
                {
                    // All coordinate work is done under per-monitor-DPI-aware context so that
                    // GetCursorPos, MonitorFromPoint, ScreenToClient, and GetDpiForMonitor all
                    // operate in physical screen pixels — correctly handling mixed-DPI setups
                    // where the invoke monitor differs from the primary monitor.
                    POINT screen_point;
                    const DPI_AWARENESS_CONTEXT prev_ctx = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

                    if (mouse_position_at_time_of_invoke.x != -1)
                    {
                        screen_point = mouse_position_at_time_of_invoke;
                    }
                    else
                    {
                        if (!GetCursorPos(&screen_point))
                            screen_point = { 100, 100 };
                    }

                    // Resolve effective DPI for the monitor the right-click was on.
                    UINT invoke_dpi_x = 96;
                    const HMONITOR h_monitor = MonitorFromPoint(screen_point, MONITOR_DEFAULTTONEAREST);
                    if (h_monitor)
                    {
                        UINT invoke_dpi_y = 0;
                        GetDpiForMonitor(h_monitor, MDT_EFFECTIVE_DPI, &invoke_dpi_x, &invoke_dpi_y);
                    }

                    // Convert physical screen coordinates to the desktop ListView's client coordinates.
                    if (desktop_window_handle)
                    {
                        ::ScreenToClient(reinterpret_cast<HWND>(static_cast<LONG_PTR>(desktop_window_handle)), &screen_point);
                    }

                    SetThreadDpiAwarenessContext(prev_ctx);

                    // Keep icon clear of the screen edge: ~30 logical pixels scaled to the invoke monitor's DPI.
                    const LONG min_margin = ::MulDiv(30, static_cast<int>(invoke_dpi_x), 96);
                    screen_point.x = std::max<LONG>(screen_point.x, min_margin);
                    screen_point.y = std::max<LONG>(screen_point.y, min_margin);

                    POINT position[] = { screen_point };
                    PCUITEMID_CHILD shell_item_to_select_and_position[] = { shell_item_id };
                    folder_view->SelectAndPositionItems(1, shell_item_to_select_and_position, position, common_select_flags | SVSI_POSITIONITEM);
                }
                else
                {
                    // Enter rename mode
                    folder_view->SelectItem(i, common_select_flags);
                }
                done = true;
            }
            CoTaskMemFree(shell_item_id);
        }
        return done;
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

    inline HRESULT copy_template(const template_item* template_entry, const ComPtr<IUnknown> site_of_folder, const POINT mouse_position_at_invoke)
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
            template_entry->enter_rename_mode(target_final_fullpath, mouse_position_at_invoke);
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

    constexpr wchar_t built_in_new_registry_path[] = LR"(Software\Classes\Directory\Background\ShellEx\ContextMenuHandlers\New)";
    constexpr wchar_t built_in_new_registry_disabled_value_prefix[] = L"disabled_";

    inline bool disable_built_in_new_via_registry()
    {
        // This is implemented to support where New+ GPO is configured to 
        // hide the built-in New context menu but Settings UI hasn't been launched
        // Mirrors the logic in DisableBuiltInNewViaRegistry in .cs

        HKEY key{};

        if (RegCreateKeyExW(HKEY_CURRENT_USER,
                            built_in_new_registry_path,
                            0,
                            nullptr,
                            REG_OPTION_NON_VOLATILE,
                            KEY_ALL_ACCESS,
                            nullptr,
                            &key,
                            nullptr) != ERROR_SUCCESS)
        {
            return false;
        }

        const auto built_in_new_registry_disabled_value_prefix_len = lstrlenW(built_in_new_registry_disabled_value_prefix);

        if (RegSetValueExW(key, nullptr, 0, REG_SZ, reinterpret_cast<const BYTE*>(&built_in_new_registry_disabled_value_prefix), built_in_new_registry_disabled_value_prefix_len) != ERROR_SUCCESS)
        {
            RegCloseKey(key);
            return true;
        }

        RegCloseKey(key);
        return false;

    }

    inline bool enable_built_in_new_via_registry()
    {
        // This is implemented to support where New+ GPO is configured to 
        // display the built-in New context menu but Settings UI hasn't been launched
        // Mirrors the logic in EnableBuiltInNewViaRegistry in .cs

        HKEY key{};

        if (RegOpenKeyExW(HKEY_CURRENT_USER,
                          built_in_new_registry_path,
                          0,
                          KEY_ALL_ACCESS,
                          &key) != ERROR_SUCCESS)
        {
            return true;
        }

        if (RegDeleteValueW(key, nullptr) != ERROR_SUCCESS)
        {
            RegCloseKey(key);
            return true;
        }

        RegCloseKey(key);
        return false;

    }
}
