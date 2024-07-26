#include "pch.h"
#include "PowerRenameRegEx.h"
#include "Enumerating.h"
#include "Settings.h"
#include <regex>
#include <string>
#include <algorithm>
#include <boost/regex.hpp>
#include <helpers.h>

using std::conditional_t;
using std::regex_error;

IFACEMETHODIMP_(ULONG)
CPowerRenameRegEx::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG)
CPowerRenameRegEx::Release()
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

HRESULT CPowerRenameRegEx::_OnEnumerateOrRandomizeItemsChanged()
{
    m_enumerators.clear();
    m_randomizer.clear();

    if (m_flags & RandomizeItems)
    {
        const auto options = parseRandomizerOptions(m_RawReplaceTerm);

        for (const auto& option : options)
        {
            m_randomizer.emplace_back(option);
        }
    }

    if (m_flags & EnumerateItems)
    {
        const auto options = parseEnumOptions(m_RawReplaceTerm);
        for (const auto& option : options)
        {
            if (m_randomizer.end() ==
                std::find_if(
                    m_randomizer.begin(),
                    m_randomizer.end(),
                    [option](const Randomizer& r) -> bool { return r.options.replaceStrSpan.offset == option.replaceStrSpan.offset; }
                ))
            {
                // Only add as enumerator if we didn't find a randomizer already at this offset.
                // Every randomizer will also be a valid enumerator according to the definition of enumerators, which allows any string to mean the default enumerator, so it should be interpreted that the user wanted a randomizer if both were found at the same offset of the replace string.
                m_enumerators.emplace_back(option);
            }
        }
    }

    m_replaceWithRandomizerOffsets.clear();
    m_replaceWithEnumeratorOffsets.clear();

    int32_t offset = 0;
    int ei = 0; // Enumerators index
    int ri = 0; // Randomizer index

    std::wstring replaceWith{ m_RawReplaceTerm };
    // Remove counter expressions and calculate their offsets in replaceWith string.

    if ((m_flags & EnumerateItems) && (m_flags & RandomizeItems))
    {
        // Both flags are on, we need to merge which ones should be applied.
        while ((ei < m_enumerators.size()) && (ri < m_randomizer.size()))
        {
            const auto& e = m_enumerators[ei];
            const auto& r = m_randomizer[ri];
            if (e.replaceStrSpan.offset < r.options.replaceStrSpan.offset)
            {
                // if the enumerator is next in line, remove counter expression and calculate offset with it.
                replaceWith.erase(e.replaceStrSpan.offset + offset, e.replaceStrSpan.length);
                m_replaceWithEnumeratorOffsets.push_back(offset);
                offset -= static_cast<int32_t>(e.replaceStrSpan.length);

                ei++;
            }
            else
            {
                // if the randomizer is next in line, remove randomizer expression and calculate offset with it.
                replaceWith.erase(r.options.replaceStrSpan.offset + offset, r.options.replaceStrSpan.length);
                m_replaceWithRandomizerOffsets.push_back(offset);
                offset -= static_cast<int32_t>(r.options.replaceStrSpan.length);

                ri++;
            }
        }
    }

    if (m_flags & EnumerateItems)
    {
        // Continue with all remaining enumerators
        while (ei < m_enumerators.size())
        {
            const auto& e = m_enumerators[ei];
            replaceWith.erase(e.replaceStrSpan.offset + offset, e.replaceStrSpan.length);
            m_replaceWithEnumeratorOffsets.push_back(offset);
            offset -= static_cast<int32_t>(e.replaceStrSpan.length);

            ei++;
        }
    }

    if (m_flags & RandomizeItems)
    {
        // Continue with all remaining randomizer instances
        while (ri < m_randomizer.size())
        {
            const auto& r = m_randomizer[ri];
            replaceWith.erase(r.options.replaceStrSpan.offset + offset, r.options.replaceStrSpan.length);
            m_replaceWithRandomizerOffsets.push_back(offset);
            offset -= static_cast<int32_t>(r.options.replaceStrSpan.length);

            ri++;
        }
    }

    return SHStrDup(replaceWith.data(), &m_replaceTerm);
}

IFACEMETHODIMP CPowerRenameRegEx::PutReplaceTerm(_In_ PCWSTR replaceTerm, bool forceRenaming)
{
    bool changed = false || forceRenaming;
    HRESULT hr = S_OK;
    if (replaceTerm)
    {
        CSRWExclusiveAutoLock lock(&m_lock);
        if (m_replaceTerm == nullptr || lstrcmp(replaceTerm, m_RawReplaceTerm.c_str()) != 0)
        {
            changed = true;
            CoTaskMemFree(m_replaceTerm);
            m_RawReplaceTerm = replaceTerm;

            if ((m_flags & RandomizeItems) || (m_flags & EnumerateItems))
                hr = _OnEnumerateOrRandomizeItemsChanged();
            else
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
        const bool newEnumerate = flags & EnumerateItems;
        const bool newRandomizer = flags & RandomizeItems;
        const bool refreshReplaceTerm =
            (!!(m_flags & EnumerateItems) != newEnumerate) ||
            (!!(m_flags & RandomizeItems) != newRandomizer);

        m_flags = flags;

        if (refreshReplaceTerm)
        {
            CSRWExclusiveAutoLock lock(&m_lock);
            if (newEnumerate || newRandomizer)
            {
                _OnEnumerateOrRandomizeItemsChanged();
            }
            else
            {
                CoTaskMemFree(m_replaceTerm);
                SHStrDup(m_RawReplaceTerm.c_str(), &m_replaceTerm);
            }
        }
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

    CPowerRenameRegEx* newRenameRegEx = new CPowerRenameRegEx();
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

template<bool Std, class Regex = conditional_t<Std, std::wregex, boost::wregex>, class Options = decltype(Regex::icase)>
static std::wstring RegexReplaceEx(const std::wstring& source, const std::wstring& searchTerm, const std::wstring& replaceTerm, const bool matchAll, const bool caseInsensitive)
{
    Regex pattern(searchTerm, Options::ECMAScript | (caseInsensitive ? Options::icase : Options{}));

    using Flags = conditional_t<Std, std::regex_constants::match_flag_type, boost::regex_constants::match_flags>;
    const auto flags = matchAll ? Flags::match_default : Flags::format_first_only;

    return regex_replace(source, pattern, replaceTerm, flags);
}

static constexpr std::array RegexReplaceDispatch = { RegexReplaceEx<true>, RegexReplaceEx<false> };

HRESULT CPowerRenameRegEx::Replace(_In_ PCWSTR source, _Outptr_ PWSTR* result, unsigned long& enumIndex)
{
    *result = nullptr;

    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = S_OK;
    if (!(m_searchTerm && wcslen(m_searchTerm) > 0 && source && wcslen(source) > 0))
    {
        return hr;
    }
    std::wstring res = source;
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

        std::wstring sourceToUse;
        std::wstring originalSource;
        sourceToUse.reserve(MAX_PATH);
        originalSource.reserve(MAX_PATH);
        sourceToUse = source;
        originalSource = sourceToUse;

        std::wstring searchTerm(m_searchTerm);
        std::wstring replaceTerm;
        if (m_useFileTime && !fileTimeErrorOccurred)
        {
            replaceTerm = newReplaceTerm;
        }
        else if (m_replaceTerm)
        {
            replaceTerm = m_replaceTerm;
        }

        static const std::wregex zeroGroupRegex(L"(([^\\$]|^)(\\$\\$)*)\\$[0]");
        static const std::wregex otherGroupsRegex(L"(([^\\$]|^)(\\$\\$)*)\\$([1-9])");

        if ((m_flags & EnumerateItems) || (m_flags & RandomizeItems))
        {
            int ei = 0; // Enumerators index
            int ri = 0; // Randomizer index
            std::array<wchar_t, MAX_PATH> buffer;
            int32_t offset = 0;

            if ((m_flags & EnumerateItems) && (m_flags & RandomizeItems))
            {
                // Both flags are on, we need to merge which ones should be applied.
                while ((ei < m_enumerators.size()) && (ri < m_randomizer.size()))
                {
                    const auto& e = m_enumerators[ei];
                    const auto& r = m_randomizer[ri];
                    if (e.replaceStrSpan.offset < r.options.replaceStrSpan.offset)
                    {
                        // if the enumerator is next in line, apply it.
                        const auto replacementLength = static_cast<int32_t>(e.printTo(buffer.data(), buffer.size(), enumIndex));
                        replaceTerm.insert(e.replaceStrSpan.offset + offset + m_replaceWithEnumeratorOffsets[ei], buffer.data());
                        offset += replacementLength;

                        ei++;
                    }
                    else
                    {
                        // if the randomizer is next in line, apply it.
                        std::string randomValue = r.randomize();
                        std::wstring wRandomValue(randomValue.begin(), randomValue.end());
                        replaceTerm.insert(r.options.replaceStrSpan.offset + offset + m_replaceWithRandomizerOffsets[ri], wRandomValue);
                        offset += static_cast<int32_t>(wRandomValue.length());

                        if (e.replaceStrSpan.offset == r.options.replaceStrSpan.offset)
                        {
                            // In theory, this shouldn't happen here as we were guarding against it when filling the randomizer and enumerator structures, but it's still here as a fail safe.
                            // Every randomizer will also be a valid enumerator according to the definition of enumerators, which allow any string to mean the default enumerator, so it should be interpreted that the user wanted a randomizer if both were found at the same offset of the replace string.
                            ei++;
                        }

                        ri++;
                    }
                }
            }

            if (m_flags & EnumerateItems)
            {
                // Replace all remaining enumerators
                while (ei < m_enumerators.size())
                {
                    const auto& e = m_enumerators[ei];
                    const auto replacementLength = static_cast<int32_t>(e.printTo(buffer.data(), buffer.size(), enumIndex));
                    replaceTerm.insert(e.replaceStrSpan.offset + offset + m_replaceWithEnumeratorOffsets[ei], buffer.data());
                    offset += replacementLength;

                    ei++;
                }
            }
            if (m_flags & RandomizeItems)
            {
                // Replace all remaining randomizer instances
                while (ri < m_randomizer.size())
                {
                    const auto& r = m_randomizer[ri];
                    std::string randomValue = r.randomize();
                    std::wstring wRandomValue(randomValue.begin(), randomValue.end());
                    replaceTerm.insert(r.options.replaceStrSpan.offset + offset + m_replaceWithRandomizerOffsets[ri], wRandomValue);
                    offset += static_cast<int32_t>(wRandomValue.length());

                    ri++;
                }
            }
        }

        bool replacedSomething = false;
        if (m_flags & UseRegularExpressions)
        {
            replaceTerm = regex_replace(replaceTerm, zeroGroupRegex, L"$1$$$0");
            replaceTerm = regex_replace(replaceTerm, otherGroupsRegex, L"$1$0$4");

            res = RegexReplaceDispatch[_useBoostLib](source, m_searchTerm, replaceTerm, m_flags & MatchAllOccurrences, !(m_flags & CaseSensitive));
            replacedSomething = originalSource != res;
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
                    replacedSomething = true;
                }
                if (!(m_flags & MatchAllOccurrences))
                {
                    break;
                }
            } while (pos != std::string::npos);
        }
        hr = SHStrDup(res.c_str(), result);
        if (replacedSomething)
            enumIndex++;
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
