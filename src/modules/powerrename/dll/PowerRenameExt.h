#pragma once
#include "pch.h"

class __declspec(uuid("0440049F-D1DC-4E46-B27B-98393D79486B")) CPowerRenameMenu :
    public IShellExtInit,
    public IContextMenu,
    public IExplorerCommand
{
public:
    CPowerRenameMenu();

    // IUnknown
    IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _COM_Outptr_ void** ppv)
    {
        static const QITAB qit[] = {
            QITABENT(CPowerRenameMenu, IShellExtInit),
            QITABENT(CPowerRenameMenu, IContextMenu),
            QITABENT(CPowerRenameMenu, IExplorerCommand),
            { 0, 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG)
    AddRef()
    {
        return ++m_refCount;
    }

    IFACEMETHODIMP_(ULONG)
    Release()
    {
        LONG refCount = --m_refCount;
        if (refCount == 0)
        {
            delete this;
        }
        return refCount;
    }

    // IShellExtInit
    STDMETHODIMP Initialize(_In_opt_ PCIDLIST_ABSOLUTE pidlFolder, _In_ IDataObject* pdto, HKEY hkProgID);

    // IContextMenu
    STDMETHODIMP QueryContextMenu(HMENU hMenu, UINT index, UINT uIDFirst, UINT uIDLast, UINT uFlags);
    STDMETHODIMP InvokeCommand(_In_ LPCMINVOKECOMMANDINFO pCMI);
    STDMETHODIMP GetCommandString(UINT_PTR, UINT, _In_opt_ UINT*, _In_ LPSTR, UINT)
    {
        return E_NOTIMPL;
    }

    HRESULT RunPowerRename(CMINVOKECOMMANDINFO* pici, IShellItemArray* psiItemArray);

    // Inherited via IExplorerCommand
    virtual HRESULT __stdcall GetTitle(IShellItemArray* psiItemArray, LPWSTR* ppszName) override;
    virtual HRESULT __stdcall GetIcon(IShellItemArray* psiItemArray, LPWSTR* ppszIcon) override;
    virtual HRESULT __stdcall GetToolTip(IShellItemArray* psiItemArray, LPWSTR* ppszInfotip) override;
    virtual HRESULT __stdcall GetCanonicalName(GUID* pguidCommandName) override;
    virtual HRESULT __stdcall GetState(IShellItemArray* psiItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState) override;
    virtual HRESULT __stdcall Invoke(IShellItemArray* psiItemArray, IBindCtx* pbc) override;
    virtual HRESULT __stdcall GetFlags(EXPCMDFLAGS* pFlags) override;
    virtual HRESULT __stdcall EnumSubCommands(IEnumExplorerCommand** ppEnum) override;

    static HRESULT s_CreateInstance(_In_opt_ IUnknown* punkOuter, _In_ REFIID riid, _Outptr_ void** ppv);

private:
    ~CPowerRenameMenu();

    std::atomic<long> m_refCount = 1;
    HBITMAP m_hbmpIcon = nullptr;
    CComPtr<IDataObject> m_spdo;
    std::wstring context_menu_caption;
};
