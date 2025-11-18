#include "pch.h"

#include "shell_context_menu.h"
#include "shell_context_sub_menu.h"
#include "shell_context_sub_menu_item.h"
#include "template_folder.h"
#include "new_utilities.h"
#include "settings.h"
#include "trace.h"
#include "Generated Files/resource.h"

using namespace Microsoft::WRL;
using namespace newplus;

#pragma region IExplorerCommand
IFACEMETHODIMP shell_context_menu::GetTitle(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_title)
{
    static const std::wstring localized_context_menu_item =
        GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_NEW, L"New+");

    return SHStrDup(localized_context_menu_item.c_str(), returned_title);
}

IFACEMETHODIMP shell_context_menu::GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_icon)
{
    *returned_icon = nullptr;

    static const auto icon_resource_filepath = utilities::get_new_icon_resource_filepath(module_instance_handle, ThemeHelpers::GetAppTheme());

    return SHStrDup(icon_resource_filepath.c_str(), returned_icon);
}

IFACEMETHODIMP shell_context_menu::GetToolTip(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_tool_tip)
{
    *returned_tool_tip = nullptr;
    return E_NOTIMPL;
}

IFACEMETHODIMP shell_context_menu::GetCanonicalName(_Out_ GUID* returned_id)
{
    *returned_id = __uuidof(this);
    return S_OK;
}

IFACEMETHODIMP shell_context_menu::GetState(_In_opt_ IShellItemArray*, _In_ BOOL, _Out_ EXPCMDSTATE* returned_state)
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

IFACEMETHODIMP shell_context_menu::Invoke(_In_opt_ IShellItemArray*, _In_opt_ IBindCtx*) noexcept
{
    return E_NOTIMPL;
}

IFACEMETHODIMP shell_context_menu::GetFlags(_Out_ EXPCMDFLAGS* returned_menu_item_flags)
{
    *returned_menu_item_flags = ECF_HASSUBCOMMANDS;
    return S_OK;
}

IFACEMETHODIMP shell_context_menu::EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** returned_enum_commands)
{
    try
    {
        auto e = Make<shell_context_sub_menu>(site_of_folder);
        return e->QueryInterface(IID_PPV_ARGS(returned_enum_commands));
    }
    catch (const std::exception& ex)
    {
        Logger::error("New+ create submenu error: {}", ex.what());
        return E_FAIL;
    }
    catch (...)
    {
        Logger::error("New+ create submenu error");
        return E_FAIL;
    }
}
#pragma endregion

#pragma region IObjectWithSite
IFACEMETHODIMP shell_context_menu::SetSite(_In_ IUnknown* site) noexcept
{
    this->site_of_folder = site;
    return S_OK;
}
IFACEMETHODIMP shell_context_menu::GetSite(_In_ REFIID interface_type, _COM_Outptr_ void** returned_site) noexcept
{
    return this->site_of_folder.CopyTo(interface_type, returned_site);
}
#pragma endregion
