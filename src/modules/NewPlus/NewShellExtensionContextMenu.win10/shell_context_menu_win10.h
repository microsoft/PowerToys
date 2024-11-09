#pragma once

#include "pch.h"
#include <template_folder.h>

using namespace Microsoft::WRL;

#define NEW_SHELL_EXTENSION_EXPLORER_COMMAND_WIN10_UUID "FF90D477-E32A-4BE8-8CC5-A502A97F5401"

// File Explorer context menu "New+" for Windows 10
class __declspec(uuid(NEW_SHELL_EXTENSION_EXPLORER_COMMAND_WIN10_UUID)) shell_context_menu_win10 :
    public RuntimeClass<
        RuntimeClassFlags<ClassicCom>,
        IShellExtInit,
        IContextMenu, 
        IObjectWithSite>
{
public:
    ~shell_context_menu_win10();

#pragma region IShellExtInit
    IFACEMETHODIMP Initialize(_In_opt_ PCIDLIST_ABSOLUTE, _In_ IDataObject*, HKEY);
#pragma endregion

#pragma region IContextMenu
    IFACEMETHODIMP QueryContextMenu(HMENU menu_handle, UINT menu_index, UINT menu_first_cmd_id, UINT, UINT menu_flags);
    IFACEMETHODIMP InvokeCommand(CMINVOKECOMMANDINFO* pici);
    IFACEMETHODIMP GetCommandString(UINT_PTR, UINT, UINT*, CHAR*, UINT);
#pragma endregion

#pragma region IObjectWithSite
    IFACEMETHODIMP SetSite(_In_ IUnknown* site) noexcept;
    IFACEMETHODIMP GetSite(_In_ REFIID riid, _COM_Outptr_ void** site) noexcept;
#pragma endregion
    
protected:
    HINSTANCE instance_handle = 0;
    ComPtr<IUnknown> site_of_folder;
    newplus::template_folder* templates = nullptr;
    std::vector<HBITMAP> bitmap_handles;
};
