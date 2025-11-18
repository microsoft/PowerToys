#include "pch.h"
#include "shell_context_sub_menu_item.h"

#include "trace.h"
#include "Generated Files/resource.h"

using namespace Microsoft::WRL;

// Sub context menu containing the actual list of templates
shell_context_sub_menu_item::shell_context_sub_menu_item()
{
    this->template_entry = nullptr;
}

shell_context_sub_menu_item::shell_context_sub_menu_item(const template_item* template_entry, const ComPtr<IUnknown> site_of_folder)
{
    this->template_entry = template_entry;
    this->site_of_folder = site_of_folder;
}

IFACEMETHODIMP shell_context_sub_menu_item::GetTitle(_In_opt_ IShellItemArray* items, _Outptr_result_nullonfailure_ PWSTR* title)
{
    return SHStrDup(this->template_entry->get_menu_title(
        !utilities::get_newplus_setting_hide_extension(),
        !utilities::get_newplus_setting_hide_starting_digits(),
        utilities::get_newplus_setting_resolve_variables()
    ).c_str(), title);
}

IFACEMETHODIMP shell_context_sub_menu_item::GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon)
{
    return SHStrDup(this->template_entry->get_explorer_icon().c_str(), icon);
}

IFACEMETHODIMP shell_context_sub_menu_item::GetToolTip(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* infoTip)
{
    *infoTip = nullptr;
    return E_NOTIMPL;
}
IFACEMETHODIMP shell_context_sub_menu_item::GetCanonicalName(_Out_ GUID* guidCommandName)
{
    *guidCommandName = GUID_NULL;
    return S_OK;
}
IFACEMETHODIMP shell_context_sub_menu_item::GetState(_In_opt_ IShellItemArray* selection, _In_ BOOL, _Out_ EXPCMDSTATE* returned_state)
{
    // Commented out for performance reasons

    //DWORD object_count = 0;
    //selection->GetCount(&object_count);

    //if (object_count == 1)
    //{
    //    *returned_state = ECS_ENABLED;
    //}
    //else
    //{
    //    *returned_state = ECS_HIDDEN;
    //}

    *returned_state = ECS_ENABLED;
    return S_OK;
}

IFACEMETHODIMP shell_context_sub_menu_item::Invoke(_In_opt_ IShellItemArray*, _In_opt_ IBindCtx*) noexcept
{
    return newplus::utilities::copy_template(template_entry, site_of_folder);
}

IFACEMETHODIMP shell_context_sub_menu_item::GetFlags(_Out_ EXPCMDFLAGS* returned_flags)
{
    *returned_flags = ECF_DEFAULT;
    return S_OK;
}

IFACEMETHODIMP shell_context_sub_menu_item::EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** enumCommands)
{
    *enumCommands = nullptr;
    return E_NOTIMPL;
}

// Sub context menu - separator
IFACEMETHODIMP separator_context_menu_item::GetTitle(_In_opt_ IShellItemArray* items, _Outptr_result_nullonfailure_ PWSTR* title)
{
    title = nullptr;

    // NOTE: Must by S_FALSE for the separator to show up
    return S_FALSE;
}

IFACEMETHODIMP separator_context_menu_item::GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon)
{
    *icon = nullptr;
    return E_NOTIMPL;
}

IFACEMETHODIMP separator_context_menu_item::GetFlags(_Out_ EXPCMDFLAGS* returned_flags)
{
    // Separators no longer work on Windows 11 regular context menu. They do still work on the extended context menu.
    *returned_flags = ECF_ISSEPARATOR;
    return S_OK;
}

// Sub context menu - "Open templates" New+ folder
template_folder_context_menu_item::template_folder_context_menu_item(const std::filesystem::path shell_template_folder)
{
    this->shell_template_folder = shell_template_folder;
}

IFACEMETHODIMP template_folder_context_menu_item::GetTitle(_In_opt_ IShellItemArray* items, _Outptr_result_nullonfailure_ PWSTR* name)
{
    static const std::wstring localized_context_menu_item =
        GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_OPEN_TEMPLATES, L"Open templates");

    return SHStrDup(localized_context_menu_item.c_str(), name);
}

IFACEMETHODIMP template_folder_context_menu_item::GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon)
{
    return SHStrDup(utilities::get_open_templates_icon_resource_filepath(module_instance_handle, ThemeHelpers::GetAppTheme()).c_str(), icon);
}

IFACEMETHODIMP template_folder_context_menu_item::Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*) noexcept
{
    return newplus::utilities::open_template_folder(shell_template_folder);
}
