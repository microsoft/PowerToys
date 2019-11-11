#pragma once
#include "stdafx.h"
#include <vector>
#include <string>
#include "srwlock.h"

#define DEFAULT_FLAGS MatchAllOccurences

class CPowerRenameRegEx : public IPowerRenameRegEx
{
public:
    // IUnknown
    IFACEMETHODIMP  QueryInterface(_In_ REFIID iid, _Outptr_ void** resultInterface);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IPowerRenameRegEx
    IFACEMETHODIMP Advise(_In_ IPowerRenameRegExEvents* regExEvents, _Out_ DWORD* cookie);
    IFACEMETHODIMP UnAdvise(_In_ DWORD cookie);
    IFACEMETHODIMP get_searchTerm(_Outptr_ PWSTR* searchTerm);
    IFACEMETHODIMP put_searchTerm(_In_ PCWSTR searchTerm);
    IFACEMETHODIMP get_replaceTerm(_Outptr_ PWSTR* replaceTerm);
    IFACEMETHODIMP put_replaceTerm(_In_ PCWSTR replaceTerm);
    IFACEMETHODIMP get_flags(_Out_ DWORD* flags);
    IFACEMETHODIMP put_flags(_In_ DWORD flags);
    IFACEMETHODIMP Replace(_In_ PCWSTR source, _Outptr_ PWSTR* result);

    static HRESULT s_CreateInstance(_Outptr_ IPowerRenameRegEx **renameRegEx);

protected:
    CPowerRenameRegEx();
    virtual ~CPowerRenameRegEx();

    void _OnSearchTermChanged();
    void _OnReplaceTermChanged();
    void _OnFlagsChanged();

    size_t _Find(std::wstring data, std::wstring toSearch, bool caseInsensitive, size_t pos);

    DWORD m_flags = DEFAULT_FLAGS;
    PWSTR m_searchTerm = nullptr;
    PWSTR m_replaceTerm = nullptr;

    CSRWLock m_lock;
    CSRWLock m_lockEvents;

    DWORD m_cookie = 0;

    struct RENAME_REGEX_EVENT
    {
        IPowerRenameRegExEvents* pEvents;
        DWORD cookie;
    };

    _Guarded_by_(m_lockEvents) std::vector<RENAME_REGEX_EVENT> m_renameRegExEvents;

    long m_refCount = 0;
};