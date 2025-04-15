#pragma once

#include "pch.h"

using namespace Microsoft::WRL;

#define NEW_SHELL_EXTENSION_EXPLORER_COMMAND_UUID_STR "69824FC6-4660-4A09-9E7C-48DA63C6CC0F"

// File Explorer context menu "New+"
class __declspec(uuid(NEW_SHELL_EXTENSION_EXPLORER_COMMAND_UUID_STR)) shell_context_menu final :
    public RuntimeClass<
        RuntimeClassFlags<ClassicCom>,
        IExplorerCommand,
        IObjectWithSite>
{
public:
#pragma region IExplorerCommand
    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_title);
    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_icon);
    IFACEMETHODIMP GetToolTip(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* returned_tool_tip);
    IFACEMETHODIMP GetCanonicalName(_Out_ GUID* returned_id);
    IFACEMETHODIMP GetState(_In_opt_ IShellItemArray*, _In_ BOOL, _Out_ EXPCMDSTATE* returned_state);
    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray*, _In_opt_ IBindCtx*) noexcept;
    IFACEMETHODIMP GetFlags(_Out_ EXPCMDFLAGS* returned_menu_item_flags);
    IFACEMETHODIMP EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** returned_enum_commands);
#pragma endregion

#pragma region IObjectWithSite
    IFACEMETHODIMP SetSite(_In_ IUnknown* site) noexcept;
    IFACEMETHODIMP GetSite(_In_ REFIID interface_type, _COM_Outptr_ void** site) noexcept;
#pragma endregion

protected:
    HINSTANCE instance_handle = 0;
    ComPtr<IUnknown> site_of_folder;
};
