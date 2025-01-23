#include "pch.h"

#include "shell_context_menu_win10.h"
#include "shell_context_sub_menu.h"
#include "shell_context_sub_menu_item.h"
#include "new_utilities.h"
#include "settings.h"
#include "trace.h"
#include "Generated Files/resource.h"
#include <common/Themes/icon_helpers.h>
#include <common/utils/winapi_error.h>

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
    return S_OK;
}
#pragma endregion

#pragma region IContextMenu
IFACEMETHODIMP shell_context_menu_win10::QueryContextMenu(HMENU menu_handle, UINT menu_index, UINT menu_first_cmd_id, UINT, UINT menu_flags)
{
    if (!NewSettingsInstance().GetEnabled() 
        || package::IsWin11OrGreater()
        )
    {
        return E_FAIL;
    }

    if (menu_flags & (CMF_DEFAULTONLY | CMF_VERBSONLY | CMF_OPTIMIZEFORINVOKE))
    {
        return E_UNEXPECTED;
    }

    try
    {
        // Create the initial context popup menu containing the list of templates and open templates action
        int menu_id = menu_first_cmd_id;
        MENUITEMINFO newplus_main_context_menu_item = { 0 };
        HMENU sub_menu_of_templates = CreatePopupMenu();
        int sub_menu_index = 0;

        // Determine the New+ Template folder location
        const std::filesystem::path template_folder_root = utilities::get_new_template_folder_location();

        // Create the New+ Template folder location if it doesn't exist (very rare scenario)
        utilities::create_folder_if_not_exist(template_folder_root);

        // Scan the folder for any files and folders (the templates)
        templates = new template_folder(template_folder_root);
        templates->rescan_template_folder();
        const auto number_of_templates = templates->list_of_templates.size();

        // Create the New+ menu item and point to the initial context popup menu
        static const std::wstring localized_context_menu_item =
            GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_NEW, L"New+");
        wchar_t newplus_menu_name[20] = { 0 };
        wcscpy_s(newplus_menu_name, ARRAYSIZE(newplus_menu_name), localized_context_menu_item.c_str());
        newplus_main_context_menu_item.cbSize = sizeof(MENUITEMINFOW);
        newplus_main_context_menu_item.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_SUBMENU;
        newplus_main_context_menu_item.wID = menu_id;
        newplus_main_context_menu_item.fType = MFT_STRING;
        newplus_main_context_menu_item.dwTypeData = (PWSTR)newplus_menu_name;
        newplus_main_context_menu_item.hSubMenu = sub_menu_of_templates;
        const auto newplus_icon_index = 0;

        if (bitmap_handles.size() == 0)
        {
            const std::wstring icon_file = utilities::get_new_icon_resource_filepath(
                                               module_instance_handle, ThemeHelpers::GetAppTheme())
                                               .c_str();
            HICON local_icon_handle = static_cast<HICON>(
                LoadImage(NULL, icon_file.c_str(), IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE));

            if (local_icon_handle)
            {
                bitmap_handles.push_back(CreateBitmapFromIcon(local_icon_handle));
                DestroyIcon(local_icon_handle);
            }
        }
        if (bitmap_handles.size() > newplus_icon_index && bitmap_handles[newplus_icon_index])
        {
            newplus_main_context_menu_item.fMask |= MIIM_BITMAP;
            newplus_main_context_menu_item.hbmpItem = bitmap_handles[newplus_icon_index];
        }

        menu_id++;

        // Add template items to context menu
        int index = 0;
        for (; index < number_of_templates; index++)
        {
            const auto template_item = templates->get_template_item(index);
            add_template_item_to_context_menu(sub_menu_of_templates, sub_menu_index, template_item, menu_id, index);
            menu_id++;
            sub_menu_index++;
        }

        // Add separator to context menu
        add_separator_to_context_menu(sub_menu_of_templates, sub_menu_index);
        sub_menu_index++;

        // Add "Open templates" item to context menu
        add_open_templates_to_context_menu(sub_menu_of_templates, sub_menu_index, template_folder_root, menu_id, index);
        menu_id++;

        if (!InsertMenuItem(menu_handle, menu_index, TRUE, &newplus_main_context_menu_item))
        {
            Logger::error(L"QueryContextMenu() failed. {}", get_last_error_or_default(GetLastError()));
            return HRESULT_FROM_WIN32(GetLastError());
        }
        else
        {
            // Return the amount if entries inserted
            const auto number_of_items_inserted = menu_id - menu_first_cmd_id;
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, number_of_items_inserted);
        }
    }
    catch (const std::exception& ex)
    {
        Logger::error(ex.what());
    }

    return E_FAIL;
}

void shell_context_menu_win10::add_open_templates_to_context_menu(HMENU sub_menu_of_templates, int sub_menu_index, const std::filesystem::path& template_folder_root, int menu_id, int index)
{
    static const std::wstring localized_context_menu_item_open_templates =
        GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_OPEN_TEMPLATES, L"Open templates");
    wchar_t menu_name_open[256] = { 0 };
    wcscpy_s(menu_name_open, ARRAYSIZE(menu_name_open), localized_context_menu_item_open_templates.c_str());
    const auto open_folder_item = Make<template_folder_context_menu_item>(template_folder_root);
    MENUITEMINFO newplus_menu_item_open_templates = { 0 };
    newplus_menu_item_open_templates.cbSize = sizeof(MENUITEMINFO);
    newplus_menu_item_open_templates.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID;
    newplus_menu_item_open_templates.wID = menu_id;
    newplus_menu_item_open_templates.fType = MFT_STRING;
    newplus_menu_item_open_templates.dwTypeData = (PWSTR)menu_name_open;

    const auto open_templates_icon_index = index + 1;
    if (bitmap_handles.size() <= open_templates_icon_index)
    {
        const std::wstring icon_file = utilities::get_open_templates_icon_resource_filepath(
                                           module_instance_handle, ThemeHelpers::GetAppTheme())
                                           .c_str();
        HICON open_template_icon_handle = static_cast<HICON>(
            LoadImage(NULL, icon_file.c_str(), IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE));
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
}

void shell_context_menu_win10::add_separator_to_context_menu(HMENU sub_menu_of_templates, int sub_menu_index)
{
    MENUITEMINFO menu_item_separator = { 0 };
    menu_item_separator.cbSize = sizeof(MENUITEMINFO);
    menu_item_separator.fMask = MIIM_FTYPE;
    menu_item_separator.fType = MFT_SEPARATOR;
    InsertMenuItem(sub_menu_of_templates, sub_menu_index, TRUE, &menu_item_separator);
}

void shell_context_menu_win10::add_template_item_to_context_menu(HMENU sub_menu_of_templates, int sub_menu_index, newplus::template_item* const template_item, int menu_id, int index)
{
    wchar_t menu_name[256] = { 0 };
    wcscpy_s(menu_name, ARRAYSIZE(menu_name), template_item->get_menu_title(
        !utilities::get_newplus_setting_hide_extension(), 
        !utilities::get_newplus_setting_hide_starting_digits(), 
        utilities::get_newplus_setting_resolve_variables()).c_str());
    MENUITEMINFO newplus_menu_item_template = { 0 };
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
}

IFACEMETHODIMP shell_context_menu_win10::InvokeCommand(CMINVOKECOMMANDINFO* params)
{
    if (!params)
    {
        return E_FAIL;
    }

    if (HIWORD(params->lpVerb)!=0)
    {
        // Not a menu command. It's likely a string verb command from another menu.
        // The logic to interpret lpVerb is explained here: https://learn.microsoft.com/en-us/previous-versions//bb776881(v=vs.85)#invokecommand-method
        return E_FAIL;
    }

    // Get selected menu item (a template or the "Open templates" item)
    const auto selected_menu_item_index = LOWORD(params->lpVerb) - 1;
    if (selected_menu_item_index < 0)
    {
        return E_FAIL;
    }

    const auto number_of_templates = templates->list_of_templates.size();
    const bool is_template_item = selected_menu_item_index < number_of_templates;

    // Save how many item templates we have so it can be sent later when we do something with New+.
    // It will be sent when the user does something, similar to Windows 11 context menu.
    newplus::utilities::set_saved_number_of_templates(static_cast<size_t>(number_of_templates));

    if (is_template_item)
    {
        // It's a template menu item
        const auto template_entry = templates->get_template_item(selected_menu_item_index);

        return newplus::utilities::copy_template(template_entry, site_of_folder);
    }
    else
    {
        // It's the "Open templates" menu item
        const std::filesystem::path template_folder_root = utilities::get_new_template_folder_location();

        return newplus::utilities::open_template_folder(template_folder_root);
    }

    return E_FAIL;
}

IFACEMETHODIMP shell_context_menu_win10::GetCommandString(UINT_PTR, UINT, UINT*, CHAR*, UINT)
{
    return E_NOTIMPL;
}
#pragma endregion

#pragma region IObjectWithSite
IFACEMETHODIMP shell_context_menu_win10::SetSite(_In_ IUnknown* site) noexcept
{
    this->site_of_folder = site;

    return S_OK;
}
IFACEMETHODIMP shell_context_menu_win10::GetSite(_In_ REFIID riid, _COM_Outptr_ void** returned_site) noexcept
{
    return this->site_of_folder.CopyTo(riid, returned_site);
}
#pragma endregion

