#pragma once

#include "pch.h"
#include "template_folder.h"
#include "template_item.h"
#include "new_utilities.h"

using namespace Microsoft::WRL;
using namespace newplus;

// The sub-context-menu that displays the list of templates
class shell_context_sub_menu_item : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand>
{
public:
    shell_context_sub_menu_item(const template_item* template_entry, const ComPtr<IUnknown> site_of_folder);

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray* items, _Outptr_result_nullonfailure_ PWSTR* title);

    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon);

    IFACEMETHODIMP GetToolTip(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* infoTip);

    IFACEMETHODIMP GetCanonicalName(_Out_ GUID* guidCommandName);

    IFACEMETHODIMP GetState(_In_opt_ IShellItemArray* selection, _In_ BOOL okToBeSlow, _Out_ EXPCMDSTATE* returned_state);

    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*) noexcept;

    IFACEMETHODIMP GetFlags(_Out_ EXPCMDFLAGS* returned_flags);

    IFACEMETHODIMP EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** enumCommands);

protected:
    shell_context_sub_menu_item();
    const template_item* template_entry;
    ComPtr<IUnknown> site_of_folder;
};

// Sub-context-menu separator between the list of templates menu-items and "Open templates" menu-item
class separator_context_menu_item final : public shell_context_sub_menu_item
{
public:
    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray* items, _Outptr_result_nullonfailure_ PWSTR* title);

    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon);

    IFACEMETHODIMP GetFlags(_Out_ EXPCMDFLAGS* returned_flags);
};

// Sub-context-menu - The "Open templates" menu-item
class template_folder_context_menu_item final : public shell_context_sub_menu_item
{
public:
    template_folder_context_menu_item(const std::filesystem::path shell_template_folder);

    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray* items, _Outptr_result_nullonfailure_ PWSTR* name);

    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon);

    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*) noexcept;

    std::filesystem::path shell_template_folder;
};
