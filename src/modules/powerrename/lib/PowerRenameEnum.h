#pragma once
#include "pch.h"
#include "PowerRenameInterfaces.h"
#include <vector>
#include "srwlock.h"

class CPowerRenameEnum :
    public IPowerRenameEnum
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(_In_ REFIID iid, _Outptr_ void** resultInterface);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // ISmartRenameEnum
    IFACEMETHODIMP Start(_In_ IEnumShellItems* enumShellItems);
    IFACEMETHODIMP Cancel();

public:
    static HRESULT s_CreateInstance(_In_ IUnknown* pdo, _In_ IPowerRenameManager* pManager, _In_ REFIID iid, _Outptr_ void** resultInterface);

protected:
    CPowerRenameEnum();
    virtual ~CPowerRenameEnum();

    HRESULT _Init(_In_ IUnknown* pdo, _In_ IPowerRenameManager* pManager);
    HRESULT _ParseEnumItems(_In_ IEnumShellItems* pesi, _In_ int depth = 0);

    CComPtr<IPowerRenameManager> m_spsrm;
    CComPtr<IUnknown> m_spdo;
    bool m_canceled = false;
    long m_refCount = 0;
};