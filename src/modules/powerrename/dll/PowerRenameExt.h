#pragma once
#include "stdafx.h"

class CPowerRenameMenu :
    public IShellExtInit,
    public IContextMenu
{
public:
    CPowerRenameMenu();

    // IUnknown
    IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _COM_Outptr_ void** ppv)
    {
        static const QITAB qit[] =
        {
            QITABENT(CPowerRenameMenu, IShellExtInit),
            QITABENT(CPowerRenameMenu, IContextMenu),
            { 0, 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef()
    {
        return InterlockedIncrement(&m_refCount);
    }

    IFACEMETHODIMP_(ULONG) Release()
    {
        LONG refCount = InterlockedDecrement(&m_refCount);
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

    static HRESULT s_CreateInstance(_In_opt_ IUnknown* punkOuter, _In_ REFIID riid, _Outptr_ void** ppv);
    static DWORD WINAPI s_PowerRenameUIThreadProc(_In_ void* pData);

    static bool SetEnabled(_In_ bool enabled);
    static bool IsEnabled();

private:
    ~CPowerRenameMenu();

    long m_refCount = 1;
    CComPtr<IDataObject> m_spdo;
};

