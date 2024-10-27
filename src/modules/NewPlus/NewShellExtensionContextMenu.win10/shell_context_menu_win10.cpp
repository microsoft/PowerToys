#include "pch.h"

#include "shell_context_menu_win10.h"
#include "shell_context_sub_menu.h"
#include "shell_context_sub_menu_item.h"
#include "template_folder.h"
#include "new_utilities.h"
#include "settings.h"
#include "trace.h"
#include "Generated Files/resource.h"
#include <common/Themes/icon_helpers.h>

using namespace Microsoft::WRL;
using namespace newplus;

#pragma region IShellExtInit
IFACEMETHODIMP shell_context_menu_win10::Initialize(PCIDLIST_ABSOLUTE, IDataObject* site, HKEY)
{
// cgaarden HACK UPDATE
    if (!NewSettingsInstance().GetEnabled())
    {
        return E_FAIL;
    }

    if (site) // cgaarden NOT sure
    {
        site_of_folder = site;
    }

    return S_OK;
}
#pragma endregion

#pragma region IExplorerCommand
// cgaarden Hack is this interface actually called on Win10???? Why the duplication????
IFACEMETHODIMP shell_context_menu_win10::GetTitle(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_title)
{
    static const std::wstring localized_context_menu_item =
        GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_NEW, L"New+");

    return SHStrDup(localized_context_menu_item.c_str(), returned_title);
}

IFACEMETHODIMP shell_context_menu_win10::GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_icon)
{
    *returned_icon = nullptr;

    static const auto icon_resource_filepath = utilities::get_new_icon_resource_filepath(module_instance_handle, ThemeHelpers::GetAppTheme());

    return SHStrDup(icon_resource_filepath.c_str(), returned_icon);
}

IFACEMETHODIMP shell_context_menu_win10::GetToolTip(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_tool_tip)
{
    *returned_tool_tip = nullptr;
    return E_NOTIMPL;
}

IFACEMETHODIMP shell_context_menu_win10::GetCanonicalName(_Out_ GUID* returned_id)
{
    *returned_id = __uuidof(this);
    return S_OK;
}

IFACEMETHODIMP shell_context_menu_win10::GetState(_In_opt_ IShellItemArray*, _In_ BOOL, _Out_ EXPCMDSTATE* returned_state)
{
    if (!NewSettingsInstance().GetEnabled())
    {
        *returned_state = ECS_HIDDEN;
    }
    else
    {
        *returned_state = ECS_ENABLED;
    }

    return S_OK;
}

IFACEMETHODIMP shell_context_menu_win10::Invoke(_In_opt_ IShellItemArray*, _In_opt_ IBindCtx*) noexcept
{
    return E_NOTIMPL;
}

IFACEMETHODIMP shell_context_menu_win10::GetFlags(_Out_ EXPCMDFLAGS* returned_menu_item_flags)
{
    *returned_menu_item_flags = ECF_HASSUBCOMMANDS;
    return S_OK;
}

IFACEMETHODIMP shell_context_menu_win10::EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** returned_enum_commands)
{
    auto e = Make<shell_context_sub_menu>(site_of_folder);
    return e->QueryInterface(IID_PPV_ARGS(returned_enum_commands));
}
#pragma endregion

#pragma region IContextMenu
IFACEMETHODIMP shell_context_menu_win10::QueryContextMenu(HMENU menu_handle, UINT menu_index, UINT menu_first_cmd_id, UINT, UINT menu_flags)
{
    if (!NewSettingsInstance().GetEnabled())
    {
        return E_FAIL;
    }

    // cgaarden NOT SURE what state of site_of_folder is
    // cgaarden Update to NOT show Win10 menu when on Windows 11++
    HRESULT hr = E_UNEXPECTED;
    if (site_of_folder && !(menu_flags & (CMF_DEFAULTONLY | CMF_VERBSONLY | CMF_OPTIMIZEFORINVOKE)))
    {
        static const std::wstring localized_context_menu_item =
            GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_NEW, L"New+");

        wchar_t menu_name[128] = { 0 };
        wcscpy_s(menu_name, ARRAYSIZE(menu_name), localized_context_menu_item.c_str());

        // cgaarden Hack refactor this code
        MENUITEMINFO mii;
        mii.cbSize = sizeof(MENUITEMINFO);
        mii.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_STATE;
        mii.wID = menu_first_cmd_id++;
        mii.fType = MFT_STRING;
        mii.dwTypeData = (PWSTR)menu_name;
        mii.fState = MFS_ENABLED;

        // icon from file
        HICON local_icon_handle = static_cast<HICON>(LoadImage(module_instance_handle, MAKEINTRESOURCE(IDI_NEWPLUS_ICON), IMAGE_ICON, 16, 16, 0));
        if (local_icon_handle)
        {
            mii.fMask |= MIIM_BITMAP;
            if (icon_handle == NULL)
            {
                icon_handle = CreateBitmapFromIcon(local_icon_handle);
            }
            mii.hbmpItem = icon_handle;
            DestroyIcon(local_icon_handle);
        }

        // cgaarden hack
        if (!InsertMenuItem(menu_handle, menu_index, TRUE, &mii))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
        }
        else
        {
            // cgaarden what does MAKE_HRESULT do? Why not just set to S_OK?
            hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        }
    }

    if (hr != S_OK)
    {
    // cgaarden Log error, if not S_SUCCESS

    }

    return hr;
}

IFACEMETHODIMP shell_context_menu_win10::InvokeCommand(CMINVOKECOMMANDINFO* pici)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP shell_context_menu_win10::GetCommandString(UINT_PTR idCmd, UINT uType, UINT* pReserved, CHAR* pszName, UINT cchMax)
{
    return E_NOTIMPL;
}
#pragma endregion

