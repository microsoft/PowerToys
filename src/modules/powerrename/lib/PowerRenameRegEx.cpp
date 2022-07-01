#include "pch.h"
#include "PowerRenameRegEx.h"
#include "Settings.h"
#include <regex>
#include <string>
#include <algorithm>
#include <boost/regex.hpp>
#include <helpers.h>

using namespace std;
using std::regex_error;

IFACEMETHODIMP_(ULONG) CPowerRenameRegEx::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG) CPowerRenameRegEx::Release()
{
    long refCount = InterlockedDecrement(&m_refCount);

    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

IFACEMETHODIMP CPowerRenameRegEx::QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(CPowerRenameRegEx, IPowerRenameRegEx),
        { 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP CPowerRenameRegEx::Advise(_In_ IPowerRenameRegExEvents* regExEvents, _Out_ DWORD* cookie)
{
    CSRWExclusiveAutoLock lock(&m_lockEvents);
    m_cookie++;
    RENAME_REGEX_EVENT srre;
    srre.cookie = m_cookie;
    srre.pEvents = regExEvents;
    regExEvents->AddRef();
    m_renameRegExEvents.push_back(srre);

    *cookie = m_cookie;

    return S_OK;
}

IFACEMETHODIMP CPowerRenameRegEx::UnAdvise(_In_ DWORD cookie)
{
    HRESULT hr = E_FAIL;
    CSRWExclusiveAutoLock lock(&m_lockEvents);

    for (std::vector<RENAME_REGEX_EVENT>::iterator it = m_renameRegExEvents.begin(); it != m_renameRegExEvents.end(); ++it)
    {
        if (it->cookie == cookie)
        {
            hr = S_OK;
            it->cookie = 0;
            if (it->pEvents)
            {
                it->pEvents->Release();
                it->pEvents = nullptr;
            }
            break;
        }
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameRegEx::GetSearchTerm(_Outptr_ PWSTR* searchTerm)
{
    *searchTerm = nullptr;
    HRESULT hr = S_OK;
    if (m_searchTerm)
    {
        CSRWSharedAutoLock lock(&m_lock);
        hr = SHStrDup(m_searchTerm, searchTerm);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameRegEx::PutSearchTerm(_In_ PCWSTR searchTerm, bool forceRenaming)
{
    bool changed = false || forceRenaming;
    HRESULT hr = S_OK;
    if (searchTerm)
    {
        CSRWExclusiveAutoLock lock(&m_lock);
        if (m_searchTerm == nullptr || lstrcmp(searchTerm, m_searchTerm) != 0)
        {
            changed = true;
            CoTaskMemFree(m_searchTerm);
            if (lstrcmp(searchTerm, L"") == 0)
            {
                m_searchTerm = NULL;
            }
            else
            {
                hr = SHStrDup(searchTerm, &m_searchTerm);
            }
        }
    }

    if (SUCCEEDED(hr) && changed)
    {
        _OnSearchTermChanged();
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameRegEx::GetReplaceTerm(_Outptr_ PWSTR* replaceTerm)
{
    *replaceTerm = nullptr;
    HRESULT hr = S_OK;
    if (m_replaceTerm)
    {
        CSRWSharedAutoLock lock(&m_lock);
        hr = SHStrDup(m_replaceTerm, replaceTerm);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameRegEx::PutReplaceTerm(_In_ PCWSTR replaceTerm, bool forceRenaming)
{
    bool changed = false || forceRenaming;
    HRESULT hr = S_OK;
    if (replaceTerm)
    {
        CSRWExclusiveAutoLock lock(&m_lock);
        if (m_replaceTerm == nullptr || lstrcmp(replaceTerm, m_replaceTerm) != 0)
        {
            changed = true;
            CoTaskMemFree(m_replaceTerm);
            hr = SHStrDup(replaceTerm, &m_replaceTerm);
        }
    }

    if (SUCCEEDED(hr) && changed)
    {
        _OnReplaceTermChanged();
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameRegEx::GetFlags(_Out_ DWORD* flags)
{
    *flags = m_flags;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameRegEx::PutFlags(_In_ DWORD flags)
{
    if (m_flags != flags)
    {
        m_flags = flags;
        _OnFlagsChanged();
    }
    return S_OK;
}

IFACEMETHODIMP CPowerRenameRegEx::PutFileTime(_In_ SYSTEMTIME fileTime)
{
    union timeunion
    {
        FILETIME fileTime;
        ULARGE_INTEGER ul;
    };

    timeunion ft1;
    timeunion ft2;

    SystemTimeToFileTime(&m_fileTime, &ft1.fileTime);
    SystemTimeToFileTime(&fileTime, &ft2.fileTime);

    if (ft2.ul.QuadPart != ft1.ul.QuadPart)
    {
        m_fileTime = fileTime;
        m_useFileTime = true;
        _OnFileTimeChanged();
    }
    return S_OK;
}

IFACEMETHODIMP CPowerRenameRegEx::ResetFileTime()
{
    SYSTEMTIME ZERO = { 0 };
    m_fileTime = ZERO;
    m_useFileTime = false;
    _OnFileTimeChanged();
    return S_OK;
}

HRESULT CPowerRenameRegEx::s_CreateInstance(_Outptr_ IPowerRenameRegEx** renameRegEx)
{
    *renameRegEx = nullptr;

    CPowerRenameRegEx *newRenameRegEx = new CPowerRenameRegEx();
    HRESULT hr = E_OUTOFMEMORY;
    if (newRenameRegEx)
    {
        hr = newRenameRegEx->QueryInterface(IID_PPV_ARGS(renameRegEx));
        newRenameRegEx->Release();
    }
    return hr;
}

CPowerRenameRegEx::CPowerRenameRegEx() :
    m_refCount(1)
{
    // Init to empty strings
    SHStrDup(L"", &m_searchTerm);
    SHStrDup(L"", &m_replaceTerm);

    _useBoostLib = CSettingsInstance().GetUseBoostLib();
}

CPowerRenameRegEx::~CPowerRenameRegEx()
{
    CoTaskMemFree(m_searchTerm);
    CoTaskMemFree(m_replaceTerm);
}

HRESULT CPowerRenameRegEx::Replace(_In_ PCWSTR source, _Outptr_ PWSTR* result)
{
    *result = nullptr;

    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = S_OK;
    if (!(m_searchTerm && wcslen(m_searchTerm) > 0 && source && wcslen(source) > 0))
    {
        return hr;
    }
    wstring res = source;
    try
    {
        // TODO: creating the regex could be costly.  May want to cache this.
        wchar_t newReplaceTerm[MAX_PATH] = { 0 };
        bool fileTimeErrorOccurred = false;
        if (m_useFileTime)
        {
            if (FAILED(GetDatedFileName(newReplaceTerm, ARRAYSIZE(newReplaceTerm), m_replaceTerm, m_fileTime)))
                fileTimeErrorOccurred = true;
        }

        std::wstring sourceToUse(source);
        std::wstring searchTerm(m_searchTerm);
        std::wstring replaceTerm(L"");
        if (m_useFileTime && !fileTimeErrorOccurred)
        {
            replaceTerm = wstring(newReplaceTerm);
        }
        else if (m_replaceTerm)
        {
            replaceTerm = wstring(m_replaceTerm);
        }

        replaceTerm = regex_replace(replaceTerm, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$[0]"), L"$1$$$0");
        replaceTerm = regex_replace(replaceTerm, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$([1-9])"), L"$1$0$4");

        if (m_flags & UseRegularExpressions)
        {
            if (_useBoostLib)
            {
                boost::wregex pattern(m_searchTerm, (!(m_flags & CaseSensitive)) ? boost::regex::icase | boost::regex::ECMAScript : boost::regex::ECMAScript);
                if (m_flags & MatchAllOccurrences)
                {
                    res = boost::regex_replace(wstring(source), pattern, replaceTerm);
                }
                else
                {
                    res = boost::regex_replace(wstring(source), pattern, replaceTerm, boost::regex_constants::format_first_only);
                }
            }
            else
            {
                std::wregex pattern(m_searchTerm, (!(m_flags & CaseSensitive)) ? regex_constants::icase | regex_constants::ECMAScript : regex_constants::ECMAScript);
                if (m_flags & MatchAllOccurrences)
                {
                    res = regex_replace(wstring(source), pattern, replaceTerm);
                }
                else
                {
                    res = regex_replace(wstring(source), pattern, replaceTerm, regex_constants::format_first_only);
                }
            }
        }
        else
        {
            // Simple search and replace
            size_t pos = 0;
            do
            {
                pos = _Find(sourceToUse, searchTerm, (!(m_flags & CaseSensitive)), pos);
                if (pos != std::string::npos)
                {
                    res = sourceToUse.replace(pos, searchTerm.length(), replaceTerm);
                    pos += replaceTerm.length();
                }

                if (!(m_flags & MatchAllOccurrences))
                {
                    break;
                }
            } while (pos != std::string::npos);
        }

        hr = SHStrDup(res.c_str(), result);
    }
    catch (regex_error e)
    {
        hr = E_FAIL;
    }
    return hr;
}

size_t CPowerRenameRegEx::_Find(std::wstring data, std::wstring toSearch, bool caseInsensitive, size_t pos)
{
    if (caseInsensitive)
    {
        // Convert to lower
        std::transform(data.begin(), data.end(), data.begin(), ::towlower);
        std::transform(toSearch.begin(), toSearch.end(), toSearch.begin(), ::towlower);
    }

    // Find sub string position in given string starting at position pos
    return data.find(toSearch, pos);
}

void CPowerRenameRegEx::_OnSearchTermChanged()
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_renameRegExEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnSearchTermChanged(m_searchTerm);
        }
    }
}

void CPowerRenameRegEx::_OnReplaceTermChanged()
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_renameRegExEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnReplaceTermChanged(m_replaceTerm);
        }
    }
}

void CPowerRenameRegEx::_OnFlagsChanged()
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_renameRegExEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnFlagsChanged(m_flags);
        }
    }
}

void CPowerRenameRegEx::_OnFileTimeChanged()
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_renameRegExEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnFileTimeChanged(m_fileTime);
        }
    }
}
