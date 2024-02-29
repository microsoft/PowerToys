#pragma once

#include "pch.h"

#include "FileLocksmithLib/IPC.h"

#define EXPLORER_COMMAND_UUID_STR "84d68575-e186-46ad-b0cb-baeb45ee29c0"

class __declspec(uuid(EXPLORER_COMMAND_UUID_STR)) ExplorerCommand : public IExplorerCommand, public IShellExtInit, public IContextMenu
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray* psiItemArray, LPWSTR* ppszName) override;
    IFACEMETHODIMP GetIcon(IShellItemArray* psiItemArray, LPWSTR* ppszIcon) override;
    IFACEMETHODIMP GetToolTip(IShellItemArray* psiItemArray, LPWSTR* ppszInfotip) override;
    IFACEMETHODIMP GetCanonicalName(GUID* pguidCommandName) override;
    IFACEMETHODIMP GetState(IShellItemArray* psiItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState) override;
    IFACEMETHODIMP Invoke(IShellItemArray* psiItemArray, IBindCtx* pbc) override;
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override;
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) override;

    // IShellExtInit
    IFACEMETHODIMP Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY hkeyProgID) override;

    // IContextMenu
    IFACEMETHODIMP QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags) override;
    IFACEMETHODIMP InvokeCommand(CMINVOKECOMMANDINFO* pici) override;
    IFACEMETHODIMP GetCommandString(UINT_PTR idCmd, UINT uType, UINT* pReserved, CHAR* pszName, UINT cchMax) override;

    // Static member to create an instance
    static HRESULT s_CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject);

    // Constructor
    ExplorerCommand();

    // Destructor
    ~ExplorerCommand();

private:
    HBITMAP m_hbmpIcon = nullptr;

    // Helpers
    HRESULT LaunchUI(CMINVOKECOMMANDINFO* pici, ipc::Writer* writer);

    std::atomic<ULONG> m_ref_count = 1;
    IDataObject* m_data_obj = NULL;
    std::wstring context_menu_caption;
};
