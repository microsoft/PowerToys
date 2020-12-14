#pragma once
#include <vector>
#include "srwlock.h"

#include "PowerRenameInterfaces.h"
class CMockPowerRenameRegExEvents :
    public IPowerRenameRegExEvents
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(_In_ REFIID iid, _Outptr_ void** resultInterface);
    IFACEMETHODIMP_(ULONG)
    AddRef();
    IFACEMETHODIMP_(ULONG)
    Release();

    // IPowerRenameRegExEvents
    IFACEMETHODIMP OnSearchTermChanged(_In_ PCWSTR searchTerm);
    IFACEMETHODIMP OnReplaceTermChanged(_In_ PCWSTR replaceTerm);
    IFACEMETHODIMP OnFlagsChanged(_In_ DWORD flags);
    IFACEMETHODIMP OnFileTimeChanged(_In_ SYSTEMTIME fileTime);

    static HRESULT s_CreateInstance(_Outptr_ IPowerRenameRegExEvents** ppsrree);

    CMockPowerRenameRegExEvents() :
        m_refCount(1)
    {
    }

    ~CMockPowerRenameRegExEvents()
    {
        CoTaskMemFree(m_searchTerm);
        CoTaskMemFree(m_replaceTerm);
    }

    PWSTR m_searchTerm = nullptr;
    PWSTR m_replaceTerm = nullptr;
    DWORD m_flags = 0;
    SYSTEMTIME m_fileTime = { 0 };
    long m_refCount;
};
