#pragma once

#include "pch.h"

using namespace Microsoft::WRL;

#define NEW_SHELL_EXTENSION_EXPLORER_COMMAND_WIN10_UUID_STR "FF90D477-E32A-4BE8-8CC5-A502A97F5401"

// File Explorer context menu "New+" for Windows 10
class __declspec(uuid(NEW_SHELL_EXTENSION_EXPLORER_COMMAND_WIN10_UUID_STR)) shell_context_menu_win10 final :
    public RuntimeClass<
        RuntimeClassFlags<ClassicCom>,
        IShellExtInit,
        IExplorerCommand,
        IContextMenu>
{
public:
#pragma region IShellExtInit
    IFACEMETHODIMP Initialize(_In_opt_ PCIDLIST_ABSOLUTE, _In_ IDataObject* site, HKEY);
#pragma endregion

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

#pragma region IContextMenu
    IFACEMETHODIMP QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags) override;
    IFACEMETHODIMP InvokeCommand(CMINVOKECOMMANDINFO* pici) override;
    IFACEMETHODIMP GetCommandString(UINT_PTR idCmd, UINT uType, UINT* pReserved, CHAR* pszName, UINT cchMax) override;
#pragma endregion

protected:
    HINSTANCE instance_handle = 0;
    HBITMAP icon_handle = nullptr;

    ComPtr<IDataObject> site_of_folder;
};
