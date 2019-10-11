#include "stdafx.h"
#include "PowerRenameRegEx.h"
#include <regex>
#include <string>
#include <algorithm>


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
    SMART_RENAME_REGEX_EVENT srre;
    srre.cookie = m_cookie;
    srre.pEvents = regExEvents;
    regExEvents->AddRef();
    m_smartRenameRegExEvents.push_back(srre);

    *cookie = m_cookie;

    return S_OK;
}

IFACEMETHODIMP CPowerRenameRegEx::UnAdvise(_In_ DWORD cookie)
{
    HRESULT hr = E_FAIL;
    CSRWExclusiveAutoLock lock(&m_lockEvents);

    for (std::vector<SMART_RENAME_REGEX_EVENT>::iterator it = m_smartRenameRegExEvents.begin(); it != m_smartRenameRegExEvents.end(); ++it)
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

IFACEMETHODIMP CPowerRenameRegEx::get_searchTerm(_Outptr_ PWSTR* searchTerm)
{
    *searchTerm = nullptr;
    HRESULT hr = m_searchTerm ? S_OK : E_FAIL;
    if (SUCCEEDED(hr))
    {
        CSRWSharedAutoLock lock(&m_lock);
        hr = SHStrDup(m_searchTerm, searchTerm);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameRegEx::put_searchTerm(_In_ PCWSTR searchTerm)
{
    bool changed = false;
    HRESULT hr = searchTerm ? S_OK : E_INVALIDARG;
    if (SUCCEEDED(hr))
    {
        CSRWExclusiveAutoLock lock(&m_lock);
        if (m_searchTerm == nullptr || lstrcmp(searchTerm, m_searchTerm) != 0)
        {
            changed = true;
            CoTaskMemFree(m_searchTerm);
            hr = SHStrDup(searchTerm, &m_searchTerm);
        }
    }

    if (SUCCEEDED(hr) && changed)
    {
        _OnSearchTermChanged();
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameRegEx::get_replaceTerm(_Outptr_ PWSTR* replaceTerm)
{
    *replaceTerm = nullptr;
    HRESULT hr = m_replaceTerm ? S_OK : E_FAIL;
    if (SUCCEEDED(hr))
    {
        CSRWSharedAutoLock lock(&m_lock);
        hr = SHStrDup(m_replaceTerm, replaceTerm);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameRegEx::put_replaceTerm(_In_ PCWSTR replaceTerm)
{
    bool changed = false;
    HRESULT hr = replaceTerm ? S_OK : E_INVALIDARG;
    if (SUCCEEDED(hr))
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

IFACEMETHODIMP CPowerRenameRegEx::get_flags(_Out_ DWORD* flags)
{
    *flags = m_flags;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameRegEx::put_flags(_In_ DWORD flags)
{
    if (m_flags != flags)
    {
        m_flags = flags;
        _OnFlagsChanged();
    }
    return S_OK;
}

HRESULT CPowerRenameRegEx::s_CreateInstance(_Outptr_ IPowerRenameRegEx** renameRegEx)
{
    *renameRegEx = nullptr;

    CPowerRenameRegEx *newRenameRegEx = new CPowerRenameRegEx();
    HRESULT hr = newRenameRegEx ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
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
    HRESULT hr = (source && wcslen(source) > 0 && m_searchTerm && wcslen(m_searchTerm) > 0) ? S_OK : E_INVALIDARG;
    if (SUCCEEDED(hr))
    {
        wstring res = source;
        try
        {
            // TODO: creating the regex could be costly.  May want to cache this.
            std::wstring sourceToUse(source);
            std::wstring searchTerm(m_searchTerm);
            std::wstring replaceTerm(m_replaceTerm ? wstring(m_replaceTerm) : wstring(L""));

            if (m_flags & UseRegularExpressions)
            {
                std::wregex pattern(m_searchTerm, (!(m_flags & CaseSensitive)) ? regex_constants::icase | regex_constants::ECMAScript : regex_constants::ECMAScript);
                if (m_flags & MatchAllOccurences)
                {
                    res = regex_replace(wstring(source), pattern, replaceTerm);
                }
                else
                {
                    std::wsmatch m;
                    if (std::regex_search(sourceToUse, m, pattern))
                    {
                        res = sourceToUse.replace(m.prefix().length(), searchTerm.length(), replaceTerm);
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

                    if (!(m_flags & MatchAllOccurences))
                    {
                        break;
                    }
                } while (pos != std::string::npos);
            }

            *result = StrDup(res.c_str());
            hr = (*result) ? S_OK : E_OUTOFMEMORY;
        }
        catch (regex_error e)
        {
            hr = E_FAIL;
        }
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

    for (std::vector<SMART_RENAME_REGEX_EVENT>::iterator it = m_smartRenameRegExEvents.begin(); it != m_smartRenameRegExEvents.end(); ++it)
    {
        if (it->pEvents)
        {
            it->pEvents->OnSearchTermChanged(m_searchTerm);
        }
    }
}

void CPowerRenameRegEx::_OnReplaceTermChanged()
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (std::vector<SMART_RENAME_REGEX_EVENT>::iterator it = m_smartRenameRegExEvents.begin(); it != m_smartRenameRegExEvents.end(); ++it)
    {
        if (it->pEvents)
        {
            it->pEvents->OnReplaceTermChanged(m_replaceTerm);
        }
    }
}

void CPowerRenameRegEx::_OnFlagsChanged()
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (std::vector<SMART_RENAME_REGEX_EVENT>::iterator it = m_smartRenameRegExEvents.begin(); it != m_smartRenameRegExEvents.end(); ++it)
    {
        if (it->pEvents)
        {
            it->pEvents->OnFlagsChanged(m_flags);
        }
    }
}
