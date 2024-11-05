#include "pch.h"

#include "shell_context_menu_win10.h"
#include "shell_context_sub_menu.h"
#include "shell_context_sub_menu_item.h"
#include "new_utilities.h"
#include "settings.h"
#include "trace.h"
#include "Generated Files/resource.h"
#include <common/Themes/icon_helpers.h>

#include <windows.h>
#include <gdiplus.h>
#pragma comment(lib, "gdiplus.lib")
using namespace Gdiplus;

using namespace Microsoft::WRL;
using namespace newplus;

shell_context_menu_win10::~shell_context_menu_win10()
{
    for (const auto& handle : bitmap_handles)
    {
        DeleteObject(handle);
    }
}


#pragma region IShellExtInit
IFACEMETHODIMP shell_context_menu_win10::Initialize(PCIDLIST_ABSOLUTE, IDataObject*, HKEY)
{
    // cgaarden HACK UPDATE- -- also check for whether win10 or not
    if (!NewSettingsInstance().GetEnabled())
    {
        return E_FAIL;
    }

    return S_OK;
}
#pragma endregion

#pragma region IContextMenu
IFACEMETHODIMP shell_context_menu_win10::QueryContextMenu(HMENU menu_handle, UINT menu_index, UINT menu_first_cmd_id, UINT menu_last_cmd_id, UINT menu_flags)
{
    // cgaarden Update to NOT show Win10 menu when on Windows 11++
    if (!NewSettingsInstance().GetEnabled())
    {
        return E_FAIL;
    }

    if (menu_flags & CMF_DEFAULTONLY)
    {
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
    }

    trace.UpdateState(true);

    const auto icon_x = GetSystemMetrics(SM_CXSMICON);
    const auto icon_y = GetSystemMetrics(SM_CYSMICON);

    // Create the initial context popup menu containing the list of templates and open templates action
    HMENU sub_menu_of_templates = CreatePopupMenu();
    int menu_id = menu_first_cmd_id;
    int sub_menu_index = 0;

    // Create the New+ menu item and point to the initial context popup menu
    static const std::wstring localized_context_menu_item =
        GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_NEW, L"New+");
    wchar_t newplus_menu_name[20] = { 0 };
    wcscpy_s(newplus_menu_name, ARRAYSIZE(newplus_menu_name), localized_context_menu_item.c_str());
    MENUITEMINFO newplus_menu_item;
    newplus_menu_item.cbSize = sizeof(MENUITEMINFO);
    newplus_menu_item.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_SUBMENU;
    newplus_menu_item.wID = menu_id;
    newplus_menu_item.fType = MFT_STRING;
    newplus_menu_item.dwTypeData = (PWSTR)newplus_menu_name;
    newplus_menu_item.hSubMenu = sub_menu_of_templates;
    const auto newplus_icon_index = 0;

    if (bitmap_handles.size() == 0)
    {
        const std::wstring icon_file = utilities::get_new_icon_resource_filepath(module_instance_handle, ThemeHelpers::GetAppTheme()).c_str();
        HICON local_icon_handle = static_cast<HICON>(LoadImage(NULL, icon_file.c_str(), IMAGE_ICON, icon_x, icon_y, LR_LOADFROMFILE));

        if (local_icon_handle)
        {
            bitmap_handles.push_back(CreateBitmapFromIcon(local_icon_handle));
            DestroyIcon(local_icon_handle);
        }
    }
    if (bitmap_handles.size() > newplus_icon_index && bitmap_handles[newplus_icon_index])
    {
        newplus_menu_item.fMask |= MIIM_BITMAP;
        newplus_menu_item.hbmpItem = bitmap_handles[newplus_icon_index];
    }

    menu_id++;

    // Determine the New+ Template folder location
    const std::filesystem::path template_folder_root = utilities::get_new_template_folder_location();

    // Create the New+ Template folder location if it doesn't exist (very rare scenario)
    utilities::create_folder_if_not_exist(template_folder_root);

    // Scan the folder for any files and folders (the templates)
    templates = new template_folder(template_folder_root);
    templates->rescan_template_folder();

    // Add template items to context menu
    const auto number_of_templates = templates->list_of_templates.size();
    int index = 0;
    for (; index < number_of_templates; index++)
    {
        const auto template_item = templates->get_template_item(index);
        wchar_t menu_name[256] = { 0 };
        wcscpy_s(menu_name, ARRAYSIZE(menu_name), template_item->get_menu_title(!utilities::get_newplus_setting_hide_extension(), !utilities::get_newplus_setting_hide_starting_digits()).c_str());
        MENUITEMINFO newplus_menu_item_template;
        newplus_menu_item_template.cbSize = sizeof(MENUITEMINFO);
        newplus_menu_item_template.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_DATA;
        newplus_menu_item_template.wID = menu_id;
        newplus_menu_item_template.fType = MFT_STRING;
        newplus_menu_item_template.dwTypeData = (PWSTR)menu_name;
        const auto current_template_icon_index = index + 1;
        if (bitmap_handles.size() <= current_template_icon_index)
        {
            HICON template_icon_handle = template_item->get_explorer_icon_handle();
            if (template_icon_handle)
            {
                bitmap_handles.push_back(CreateBitmapFromIcon(template_icon_handle));
                DestroyIcon(template_icon_handle);
            }
        }
        if (bitmap_handles.size() > current_template_icon_index && bitmap_handles[current_template_icon_index])
        {
            newplus_menu_item_template.fMask |= MIIM_BITMAP;
            newplus_menu_item_template.hbmpItem = bitmap_handles[current_template_icon_index];
        }

        InsertMenuItem(sub_menu_of_templates, sub_menu_index, TRUE, &newplus_menu_item_template);
        menu_id++;
        sub_menu_index++;
    }


    // Add separator to context menu
    MENUITEMINFO menu_item_separator;
    menu_item_separator.cbSize = sizeof(MENUITEMINFO);
    menu_item_separator.fMask = MIIM_FTYPE;
    menu_item_separator.fType = MFT_SEPARATOR;
    InsertMenuItem(sub_menu_of_templates, sub_menu_index, TRUE, &menu_item_separator);
    sub_menu_index++;

    // Add "Open templates" item to context menu
    static const std::wstring localized_context_menu_item_open_templates =
        GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_OPEN_TEMPLATES, L"Open templates");
    wchar_t menu_name_open[256] = { 0 };
    wcscpy_s(menu_name_open, ARRAYSIZE(menu_name_open), localized_context_menu_item_open_templates.c_str());
    const auto open_folder_item = Make<template_folder_context_menu_item>(template_folder_root);
    MENUITEMINFO newplus_menu_item_open_templates;
    newplus_menu_item_open_templates.cbSize = sizeof(MENUITEMINFO);
    newplus_menu_item_open_templates.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID;
    newplus_menu_item_open_templates.wID = menu_id;
    newplus_menu_item_open_templates.fType = MFT_STRING;
    newplus_menu_item_open_templates.dwTypeData = (PWSTR)menu_name_open;

    const auto open_templates_icon_index = index + 1;
    if (bitmap_handles.size() <= open_templates_icon_index)
    {
        const std::wstring icon_file = utilities::get_open_templates_icon_resource_filepath(module_instance_handle, ThemeHelpers::GetAppTheme()).c_str();
        HICON open_template_icon_handle = static_cast<HICON>(LoadImage(NULL, icon_file.c_str(), IMAGE_ICON, icon_x, icon_y, LR_LOADFROMFILE));
        if (open_template_icon_handle)
        {
            bitmap_handles.push_back(CreateBitmapFromIcon(open_template_icon_handle));
            DestroyIcon(open_template_icon_handle);
        }
    }
    if (bitmap_handles.size() > open_templates_icon_index && bitmap_handles[open_templates_icon_index])
    {
        newplus_menu_item_open_templates.fMask |= MIIM_BITMAP;
        newplus_menu_item_open_templates.hbmpItem = bitmap_handles[open_templates_icon_index];
    }

    InsertMenuItem(sub_menu_of_templates, sub_menu_index, TRUE, &newplus_menu_item_open_templates);
    menu_id++;


    // Log that context menu was shown and with how many items
    Trace::EventShowTemplateItems(number_of_templates);

    trace.Flush();
    trace.UpdateState(false);

    if (!InsertMenuItem(menu_handle, menu_index, TRUE, &newplus_menu_item))
    {
        return HRESULT_FROM_WIN32(GetLastError());
        // cgaarden Log error, if not S_SUCCESS
    }
    else
    {
        const auto number_of_items_inserted = menu_id - menu_first_cmd_id;
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, number_of_items_inserted);
    }
}

IFACEMETHODIMP shell_context_menu_win10::InvokeCommand(CMINVOKECOMMANDINFO* params)
{
    if (!params)
    {
        return E_FAIL;
    }

    const auto selected_menu_item_index = LOWORD(params->lpVerb) - 1;
    if (selected_menu_item_index < 0)
    {
        return E_FAIL;
    }

    trace.UpdateState(true);

    HRESULT hr = S_OK;
    const auto number_of_templates = templates->list_of_templates.size();
    const bool is_template_item = selected_menu_item_index < number_of_templates;

    if (is_template_item)
    {
        // it's a template item

        try
        {
            Logger::info(L"Copying template");

            const auto template_entry = templates->get_template_item(selected_menu_item_index);

            // Determine target path of where context menu was displayed
            const auto target_path_name = utilities::get_path_from_folder_view(target_folder_view);

            // Determine initial filename
            std::filesystem::path source_fullpath = template_entry->path;
            std::filesystem::path target_fullpath = std::wstring(target_path_name);

            // Only append name to target if source is not a directory
            if (!utilities::is_directory(source_fullpath))
            {
                target_fullpath.append(template_entry->get_target_filename(!utilities::get_newplus_setting_hide_starting_digits()));
            }

            // Copy file and determine final filename
            std::filesystem::path target_final_fullpath = template_entry->copy_object_to(GetActiveWindow(), target_fullpath);

            Trace::EventCopyTemplate(target_final_fullpath.extension().c_str());

            // Refresh folder items
            SHChangeNotify(SHCNE_CREATE, SHCNF_PATH | SHCNF_FLUSH, target_final_fullpath.wstring().c_str(), NULL);

            // Enter rename mode
            template_entry->enter_rename_mode(target_folder_view, target_final_fullpath);

            Trace::EventCopyTemplateResult(S_OK);
        }
        catch (const std::exception& ex)
        {
            Trace::EventCopyTemplateResult(S_FALSE);
            Logger::error(ex.what());

            hr = S_FALSE;
        }
    }
    else
    {
        // it's the "Open templates" item
        Logger::info(L"Open templates folder");
        const std::filesystem::path template_folder_root = utilities::get_new_template_folder_location();
        const std::wstring verb_hardcoded_do_not_change = L"open";
        ShellExecute(nullptr, verb_hardcoded_do_not_change.c_str(), template_folder_root.c_str(), NULL, NULL, SW_SHOWNORMAL);

        Trace::EventOpenTemplates();
    }

    trace.Flush();
    trace.UpdateState(false);

    return hr;
}

IFACEMETHODIMP shell_context_menu_win10::GetCommandString(UINT_PTR, UINT, UINT*, CHAR*, UINT)
{
    return E_NOTIMPL;
}
#pragma endregion

#pragma region IObjectWithSite
IFACEMETHODIMP shell_context_menu_win10::SetSite(_In_ IUnknown* site) noexcept
{
    if (site)
    {
        this->site_of_folder = site;
        ComPtr<IServiceProvider> service_provider;
        site->QueryInterface(IID_PPV_ARGS(&service_provider));
        service_provider->QueryService(__uuidof(IFolderView), IID_PPV_ARGS(&target_folder_view));
    }

    return S_OK;
}
IFACEMETHODIMP shell_context_menu_win10::GetSite(_In_ REFIID riid, _COM_Outptr_ void** returned_site) noexcept
{
    return this->site_of_folder.CopyTo(riid, returned_site);
}
#pragma endregion
