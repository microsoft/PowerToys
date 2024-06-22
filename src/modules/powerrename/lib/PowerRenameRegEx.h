#pragma once
#include "pch.h"
#include "srwlock.h"

#include "Enumerating.h"

#include "Randomizer.h"

#include "PowerRenameInterfaces.h"

#define DEFAULT_FLAGS 0

class CPowerRenameRegEx : public IPowerRenameRegEx
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(_In_ REFIID iid, _Outptr_ void** resultInterface);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IPowerRenameRegEx
    IFACEMETHODIMP Advise(_In_ IPowerRenameRegExEvents* regExEvents, _Out_ DWORD* cookie);
    IFACEMETHODIMP UnAdvise(_In_ DWORD cookie);
    IFACEMETHODIMP GetSearchTerm(_Outptr_ PWSTR* searchTerm);
    IFACEMETHODIMP PutSearchTerm(_In_ PCWSTR searchTerm, bool forceRenaming);
    IFACEMETHODIMP GetReplaceTerm(_Outptr_ PWSTR* replaceTerm);
    IFACEMETHODIMP PutReplaceTerm(_In_ PCWSTR replaceTerm, bool forceRenaming);
    IFACEMETHODIMP GetFlags(_Out_ DWORD* flags);
    IFACEMETHODIMP PutFlags(_In_ DWORD flags);
    IFACEMETHODIMP PutFileTime(_In_ SYSTEMTIME fileTime);
    IFACEMETHODIMP ResetFileTime();
    IFACEMETHODIMP Replace(_In_ PCWSTR source, _Outptr_ PWSTR* result, unsigned long& enumIndex);

    static HRESULT s_CreateInstance(_Outptr_ IPowerRenameRegEx** renameRegEx);

protected:
    CPowerRenameRegEx();
    virtual ~CPowerRenameRegEx();

    void _OnSearchTermChanged();
    void _OnReplaceTermChanged();
    void _OnFlagsChanged();
    void _OnFileTimeChanged();
    HRESULT _OnEnumerateOrRandomizeItemsChanged();

    size_t _Find(std::wstring data, std::wstring toSearch, bool caseInsensitive, size_t pos);

    bool _useBoostLib = false;
    DWORD m_flags = DEFAULT_FLAGS;
    PWSTR m_searchTerm = nullptr;
    PWSTR m_replaceTerm = nullptr;
    std::wstring m_RawReplaceTerm; 

    SYSTEMTIME m_fileTime = { 0 };
    bool m_useFileTime = false;

    CSRWLock m_lock;
    CSRWLock m_lockEvents;

    DWORD m_cookie = 0;

    std::vector<Enumerator> m_enumerators;
    std::vector<int32_t> m_replaceWithEnumeratorOffsets;

    std::vector<Randomizer> m_randomizer;
    std::vector<int32_t> m_replaceWithRandomizerOffsets;

    struct RENAME_REGEX_EVENT
    {
        IPowerRenameRegExEvents* pEvents;
        DWORD cookie;
    };

    _Guarded_by_(m_lockEvents) std::vector<RENAME_REGEX_EVENT> m_renameRegExEvents;

    long m_refCount = 0;
};