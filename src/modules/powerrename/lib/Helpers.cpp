#include "pch.h"
#include "Helpers.h"
#include "MetadataTypes.h"
#include <regex>
#include <ShlGuid.h>
#include <cstring>
#include <filesystem>
#include <unordered_map>
#include <unordered_set>
#include <algorithm>

namespace fs = std::filesystem;

namespace
{
    const int MAX_INPUT_STRING_LEN = 1024;

    const wchar_t c_rootRegPath[] = L"Software\\Microsoft\\PowerRename";

    // Helper function: Find the longest matching pattern starting at the given position
    // Returns the matched pattern name, or empty string if no match found
    std::wstring FindLongestPattern(
        const std::wstring& input,
        size_t startPos,
        size_t maxPatternLength,
        const std::unordered_set<std::wstring>& validPatterns)
    {
        const size_t remaining = input.length() - startPos;
        const size_t searchLength = std::min(maxPatternLength, remaining);

        // Try to match from longest to shortest to ensure greedy matching
        // e.g., DATE_TAKEN_YYYY should be matched before DATE_TAKEN_YY
        for (size_t len = searchLength; len > 0; --len)
        {
            std::wstring candidate = input.substr(startPos, len);
            if (validPatterns.find(candidate) != validPatterns.end())
            {
                return candidate;
            }
        }

        return L"";
    }

    // Helper function: Get the replacement value for a pattern
    // Returns the actual metadata value if available; if not, returns the pattern name with $ prefix
    std::wstring GetPatternValue(
        const std::wstring& patternName,
        const PowerRenameLib::MetadataPatternMap& patterns)
    {
        auto it = patterns.find(patternName);

        // Return actual value if found and valid (non-empty)
        if (it != patterns.end() && !it->second.empty())
        {
            return it->second;
        }

        // Return pattern name with $ prefix if value is unavailable
        // This provides visual feedback that the field exists but has no data
        return L"$" + patternName;
    }
}

HRESULT GetTrimmedFileName(_Out_ PWSTR result, UINT cchMax, _In_ PCWSTR source)
{
    HRESULT hr = E_INVALIDARG;
    if (source)
    {
        PWSTR newName = nullptr;
        hr = SHStrDup(source, &newName);
        if (SUCCEEDED(hr))
        {
            size_t firstValidIndex = 0, lastValidIndex = wcslen(newName) - 1;
            while (firstValidIndex <= lastValidIndex && iswspace(newName[firstValidIndex]))
            {
                firstValidIndex++;
            }
            while (firstValidIndex <= lastValidIndex && (iswspace(newName[lastValidIndex]) || newName[lastValidIndex] == L'.'))
            {
                lastValidIndex--;
            }
            newName[lastValidIndex + 1] = '\0';

            hr = StringCchCopy(result, cchMax, newName + firstValidIndex);
        }
        CoTaskMemFree(newName);
    }

    return hr;
}

HRESULT GetTransformedFileName(_Out_ PWSTR result, UINT cchMax, _In_ PCWSTR source, DWORD flags, bool isFolder)
{
    std::locale::global(std::locale(""));
    HRESULT hr = E_INVALIDARG;

    auto contractionOrSingleQuotedWordCheck = [](std::wstring stem, size_t i) {
        return !i || stem[i - 1] != '\'' || (i == 1 || iswpunct(stem[i - 2]) || iswspace(stem[i - 2]));
    };

    if (source && flags)
    {
        if (flags & Uppercase)
        {
            if (isFolder)
            {
                hr = StringCchCopy(result, cchMax, source);
                if (SUCCEEDED(hr))
                {
                    std::transform(result, result + wcslen(result), result, ::towupper);
                }
            }
            else
            {
                if (flags & NameOnly)
                {
                    std::wstring stem = fs::path(source).stem().wstring();
                    std::transform(stem.begin(), stem.end(), stem.begin(), ::towupper);
                    hr = StringCchPrintf(result, cchMax, L"%s%s", stem.c_str(), fs::path(source).extension().c_str());
                }
                else if (flags & ExtensionOnly)
                {
                    std::wstring extension = fs::path(source).extension().wstring();
                    if (!extension.empty())
                    {
                        std::transform(extension.begin(), extension.end(), extension.begin(), ::towupper);
                        hr = StringCchPrintf(result, cchMax, L"%s%s", fs::path(source).stem().c_str(), extension.c_str());
                    }
                    else
                    {
                        hr = StringCchCopy(result, cchMax, source);
                        if (SUCCEEDED(hr))
                        {
                            std::transform(result, result + wcslen(result), result, ::towupper);
                        }
                    }
                }
                else
                {
                    hr = StringCchCopy(result, cchMax, source);
                    if (SUCCEEDED(hr))
                    {
                        std::transform(result, result + wcslen(result), result, ::towupper);
                    }
                }
            }
        }
        else if (flags & Lowercase)
        {
            if (isFolder)
            {
                hr = StringCchCopy(result, cchMax, source);
                if (SUCCEEDED(hr))
                {
                    std::transform(result, result + wcslen(result), result, ::towlower);
                }
            }
            else
            {
                if (flags & NameOnly)
                {
                    std::wstring stem = fs::path(source).stem().wstring();
                    std::transform(stem.begin(), stem.end(), stem.begin(), ::towlower);
                    hr = StringCchPrintf(result, cchMax, L"%s%s", stem.c_str(), fs::path(source).extension().c_str());
                }
                else if (flags & ExtensionOnly)
                {
                    std::wstring extension = fs::path(source).extension().wstring();
                    if (!extension.empty())
                    {
                        std::transform(extension.begin(), extension.end(), extension.begin(), ::towlower);
                        hr = StringCchPrintf(result, cchMax, L"%s%s", fs::path(source).stem().c_str(), extension.c_str());
                    }
                    else
                    {
                        hr = StringCchCopy(result, cchMax, source);
                        if (SUCCEEDED(hr))
                        {
                            std::transform(result, result + wcslen(result), result, ::towlower);
                        }
                    }
                }
                else
                {
                    hr = StringCchCopy(result, cchMax, source);
                    if (SUCCEEDED(hr))
                    {
                        std::transform(result, result + wcslen(result), result, ::towlower);
                    }
                }
            }
        }
        else if (flags & Titlecase)
        {
            if (!(flags & ExtensionOnly))
            {
                std::vector<std::wstring> exceptions = { L"a", L"an", L"to", L"the", L"at", L"by", L"for", L"in", L"of", L"on", L"up", L"and", L"as", L"but", L"or", L"nor" };
                std::wstring stem = isFolder ? source : fs::path(source).stem().wstring();
                std::wstring extension = isFolder ? L"" : fs::path(source).extension().wstring();

                size_t stemLength = stem.length();
                bool isFirstWord = true;

                while (stemLength > 0 && (iswspace(stem[stemLength - 1]) || iswpunct(stem[stemLength - 1])))
                {
                    stemLength--;
                }

                for (size_t i = 0; i < stemLength; i++)
                {
                    if (!i || iswspace(stem[i - 1]) || (iswpunct(stem[i - 1]) && contractionOrSingleQuotedWordCheck(stem, i)))
                    {
                        if (iswspace(stem[i]) || iswpunct(stem[i]))
                        {
                            continue;
                        }
                        size_t wordLength = 0;
                        while (i + wordLength < stemLength && !iswspace(stem[i + wordLength]) && !iswpunct(stem[i + wordLength]))
                        {
                            wordLength++;
                        }

                        auto subStr = stem.substr(i, wordLength);
                        std::transform(subStr.begin(), subStr.end(), subStr.begin(), ::towlower);
                        if (isFirstWord || i + wordLength == stemLength || std::find(exceptions.begin(), exceptions.end(), subStr) == exceptions.end())
                        {
                            stem[i] = towupper(stem[i]);
                            isFirstWord = false;
                        }
                        else
                        {
                            stem[i] = towlower(stem[i]);
                        }
                    }
                    else
                    {
                        stem[i] = towlower(stem[i]);
                    }
                }
                hr = StringCchPrintf(result, cchMax, L"%s%s", stem.c_str(), extension.c_str());
            }
            else
            {
                hr = StringCchCopy(result, cchMax, source);
            }
        }
        else if (flags & Capitalized)
        {
            if (!(flags & ExtensionOnly))
            {
                std::wstring stem = isFolder ? source : fs::path(source).stem().wstring();
                std::wstring extension = isFolder ? L"" : fs::path(source).extension().wstring();

                size_t stemLength = stem.length();

                while (stemLength > 0 && (iswspace(stem[stemLength - 1]) || iswpunct(stem[stemLength - 1])))
                {
                    stemLength--;
                }

                for (size_t i = 0; i < stemLength; i++)
                {
                    if (!i || iswspace(stem[i - 1]) || (iswpunct(stem[i - 1]) && contractionOrSingleQuotedWordCheck(stem, i)))
                    {
                        if (iswspace(stem[i]) || iswpunct(stem[i]))
                        {
                            continue;
                        }
                        else
                        {
                            stem[i] = towupper(stem[i]);
                        }
                    }
                    else
                    {
                        stem[i] = towlower(stem[i]);
                    }
                }
                hr = StringCchPrintf(result, cchMax, L"%s%s", stem.c_str(), extension.c_str());
            }
            else
            {
                hr = StringCchCopy(result, cchMax, source);
            }
        }
        else
        {
            hr = StringCchCopy(result, cchMax, source);
        }
    }

    return hr;
}

bool isFileTimeUsed(_In_ PCWSTR source)
{
    bool used = false;
    static const std::array patterns = {
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$Y" }, 
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$M" }, 
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$D" }, 
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$h" }, 
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$m" }, 
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$s" }, 
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$f" },
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$H" },
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$T" },
        std::wregex{ L"(([^\\$]|^)(\\$\\$)*)\\$t" }
    };
    
    for (size_t i = 0; !used && i < patterns.size(); i++)
    {
        if (std::regex_search(source, patterns[i]))
        {
            used = true;
        }
    }
    return used;
}

bool isMetadataUsed(_In_ PCWSTR source, PowerRenameLib::MetadataType metadataType, _In_opt_ PCWSTR filePath, bool isFolder)
{
    if (!source) return false;
    
    // Early exit: If file path is provided, check file type first (fastest checks)
    // This avoids expensive pattern matching for files that don't support metadata
    if (filePath != nullptr)
    {
        // Folders don't support metadata extraction
        if (isFolder)
        {
            return false;
        }

        // Check if file path is valid
        if (wcslen(filePath) == 0)
        {
            return false;
        }

        // Get file extension
        std::wstring extension = fs::path(filePath).extension().wstring();
        
        // Convert to lowercase for case-insensitive comparison
        std::transform(extension.begin(), extension.end(), extension.begin(), ::towlower);

        // According to the metadata support table, only these formats support metadata extraction:
        // - JPEG (IFD, Exif, XMP, GPS, IPTC) - supports fast metadata encoding
        // - TIFF (IFD, Exif, XMP, GPS, IPTC) - supports fast metadata encoding  
        // - PNG (text chunks)
        static const std::unordered_set<std::wstring> supportedExtensions = {
            L".jpg",
            L".jpeg",
            L".png",
            L".tif",
            L".tiff"
        };

        // If file type doesn't support metadata, no need to check patterns
        if (supportedExtensions.find(extension) == supportedExtensions.end())
        {
            return false;
        }
    }
    
    // Now check if any metadata pattern exists in the source string
    // This is the most expensive check, so we do it last
    std::wstring str(source);
    
    // Get supported patterns for the specified metadata type
    auto supportedPatterns = PowerRenameLib::MetadataPatternExtractor::GetSupportedPatterns(metadataType);
    
    // Check if any metadata pattern exists in the source string
    for (const auto& pattern : supportedPatterns)
    {
        std::wstring searchPattern = L"$" + pattern;
        if (str.find(searchPattern) != std::wstring::npos)
        {
            return true;
        }
    }
    
    // No metadata pattern found
    return false;
}

HRESULT GetDatedFileName(_Out_ PWSTR result, UINT cchMax, _In_ PCWSTR source, SYSTEMTIME fileTime)
{
    std::locale::global(std::locale(""));
    HRESULT hr = E_INVALIDARG;
    if (source && wcslen(source) > 0)
    {
        std::wstring res(source);
        wchar_t replaceTerm[MAX_PATH] = { 0 };
        wchar_t formattedDate[MAX_PATH] = { 0 };

        wchar_t localeName[LOCALE_NAME_MAX_LENGTH];
        if (GetUserDefaultLocaleName(localeName, LOCALE_NAME_MAX_LENGTH) == 0)
        {
            StringCchCopy(localeName, LOCALE_NAME_MAX_LENGTH, L"en_US");
        }

        int hour12 = (fileTime.wHour % 12);
        if (hour12 == 0)
        {
            hour12 = 12;
        }

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%04d"), L"$01", fileTime.wYear);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$YYYY"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", (fileTime.wYear % 100));
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$YY(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $YYY, $YYYY, or metadata patterns

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", (fileTime.wYear % 10));
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$Y(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $YY, $YYYY, or metadata patterns

        GetDateFormatEx(localeName, NULL, &fileTime, L"MMMM", formattedDate, MAX_PATH, NULL);
        formattedDate[0] = towupper(formattedDate[0]);
        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", formattedDate);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$MMMM"), replaceTerm);

        GetDateFormatEx(localeName, NULL, &fileTime, L"MMM", formattedDate, MAX_PATH, NULL);
        formattedDate[0] = towupper(formattedDate[0]);
        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", formattedDate);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$MMM(?!M)"), replaceTerm); // Negative lookahead prevents matching $MMMM

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wMonth);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$MM(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $MMM, $MMMM, or metadata patterns

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wMonth);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$M(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $MM, $MMM, $MMMM, or metadata patterns

        GetDateFormatEx(localeName, NULL, &fileTime, L"dddd", formattedDate, MAX_PATH, NULL);
        formattedDate[0] = towupper(formattedDate[0]);
        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", formattedDate);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$DDDD"), replaceTerm);

        GetDateFormatEx(localeName, NULL, &fileTime, L"ddd", formattedDate, MAX_PATH, NULL);
        formattedDate[0] = towupper(formattedDate[0]);
        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", formattedDate);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$DDD(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $DDDD or metadata patterns

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wDay);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$DD(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $DDD, $DDDD, or metadata patterns

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wDay);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$D(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $DD, $DDD, $DDDD, or metadata patterns like $DATE_TAKEN_YYYY

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", hour12);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$HH(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $HHH or metadata patterns

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", hour12);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$H(?![A-Z])"), replaceTerm); // Negative lookahead prevents matching $HH or metadata patterns

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", (fileTime.wHour < 12) ? L"AM" : L"PM");
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$TT"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", (fileTime.wHour < 12) ? L"am" : L"pm");
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$tt"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wHour);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$hh(?!h)"), replaceTerm); // Negative lookahead prevents matching $hhh

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wHour);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$h(?!h)"), replaceTerm); // Negative lookahead prevents matching $hh

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wMinute);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$mm(?!m)"), replaceTerm); // Negative lookahead prevents matching $mmm

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wMinute);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$m(?!m)"), replaceTerm); // Negative lookahead prevents matching $mm

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wSecond);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$ss(?!s)"), replaceTerm); // Negative lookahead prevents matching $sss

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wSecond);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$s(?!s)"), replaceTerm); // Negative lookahead prevents matching $ss

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%03d"), L"$01", fileTime.wMilliseconds);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$fff(?!f)"), replaceTerm); // Negative lookahead prevents matching $ffff

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wMilliseconds / 10);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$ff(?!f)"), replaceTerm); // Negative lookahead prevents matching $fff

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wMilliseconds / 100);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$f(?!f)"), replaceTerm); // Negative lookahead prevents matching $ff or $fff

        hr = StringCchCopy(result, cchMax, res.c_str());
    }

    return hr;
}

HRESULT GetMetadataFileName(_Out_ PWSTR result, UINT cchMax, _In_ PCWSTR source, const PowerRenameLib::MetadataPatternMap& patterns)
{
    if (!source || wcslen(source) == 0)
    {
        return E_INVALIDARG;
    }

    std::wstring input(source);
    std::wstring output;
    output.reserve(input.length() * 2); // Reserve space to avoid frequent reallocations

    // Build pattern lookup table for fast validation
    // Using all possible patterns to recognize valid pattern names even when metadata is unavailable
    auto allPatterns = PowerRenameLib::MetadataPatternExtractor::GetAllPossiblePatterns();
    std::unordered_set<std::wstring> validPatterns;
    validPatterns.reserve(allPatterns.size());
    size_t maxPatternLength = 0;
    for (const auto& pattern : allPatterns)
    {
        validPatterns.insert(pattern);
        maxPatternLength = std::max(maxPatternLength, pattern.length());
    }

    size_t pos = 0;
    while (pos < input.length())
    {
        // Handle regular characters
        if (input[pos] != L'$')
        {
            output += input[pos];
            pos++;
            continue;
        }

        // Count consecutive dollar signs
        size_t dollarCount = 0;
        while (pos < input.length() && input[pos] == L'$')
        {
            dollarCount++;
            pos++;
        }

        // Even number of dollars: all are escaped (e.g., $$ -> $, $$$$ -> $$)
        if (dollarCount % 2 == 0)
        {
            output.append(dollarCount / 2, L'$');
            continue;
        }

        // Odd number of dollars: pairs are escaped, last one might be a pattern prefix
        // e.g., $ -> might be pattern, $$$ -> $ + might be pattern
        size_t escapedDollars = dollarCount / 2;

        // If no more characters, output all dollar signs
        if (pos >= input.length())
        {
            output.append(dollarCount, L'$');
            continue;
        }

        // Try to match a pattern (greedy matching for longest pattern)
        std::wstring matchedPattern = FindLongestPattern(input, pos, maxPatternLength, validPatterns);

        if (matchedPattern.empty())
        {
            // No pattern matched, output all dollar signs
            output.append(dollarCount, L'$');
        }
        else
        {
            // Pattern matched
            output.append(escapedDollars, L'$'); // Output escaped dollars first

            // Replace pattern with its value or keep pattern name if value unavailable
            std::wstring replacementValue = GetPatternValue(matchedPattern, patterns);
            output += replacementValue;

            pos += matchedPattern.length();
        }
    }

    return StringCchCopy(result, cchMax, output.c_str());
}


HRESULT GetShellItemArrayFromDataObject(_In_ IUnknown* dataSource, _COM_Outptr_ IShellItemArray** items)
{
    *items = nullptr;
    CComPtr<IDataObject> dataObj;
    HRESULT hr;
    if (SUCCEEDED(dataSource->QueryInterface(IID_PPV_ARGS(&dataObj))))
    {
        hr = SHCreateShellItemArrayFromDataObject(dataObj, IID_PPV_ARGS(items));
    }
    else
    {
        hr = dataSource->QueryInterface(IID_PPV_ARGS(items));
    }

    return hr;
}

BOOL GetEnumeratedFileName(__out_ecount(cchMax) PWSTR pszUniqueName, UINT cchMax, __in PCWSTR pszTemplate, __in_opt PCWSTR pszDir, unsigned long ulMinLong, __inout unsigned long* pulNumUsed)
{
    PWSTR pszName = nullptr;
    HRESULT hr = S_OK;
    BOOL fRet = FALSE;
    int cchDir = 0;

    if (0 != cchMax && pszUniqueName)
    {
        *pszUniqueName = 0;
        if (pszDir)
        {
            hr = StringCchCopy(pszUniqueName, cchMax, pszDir);
            if (SUCCEEDED(hr))
            {
                hr = PathCchAddBackslashEx(pszUniqueName, cchMax, &pszName, nullptr);
                if (SUCCEEDED(hr))
                {
                    cchDir = lstrlen(pszDir);
                }
            }
        }
        else
        {
            cchDir = 0;
            pszName = pszUniqueName;
        }
    }
    else
    {
        hr = E_INVALIDARG;
    }

    int cchTmp = 0;
    int cchStem = 0;
    PCWSTR pszStem = nullptr;
    PCWSTR pszRest = nullptr;
    wchar_t szFormat[MAX_PATH] = { 0 };

    if (SUCCEEDED(hr))
    {
        pszStem = pszTemplate;

        pszRest = StrChr(pszTemplate, L'(');
        while (pszRest)
        {
            PCWSTR pszEndUniq = CharNext(pszRest);
            while (*pszEndUniq && *pszEndUniq >= L'0' && *pszEndUniq <= L'9')
            {
                pszEndUniq++;
            }

            if (*pszEndUniq == L')' && (*CharNext(pszEndUniq) == L'\0' || CharNext(pszEndUniq) == PathFindExtension(pszTemplate)))
            {
                break;
            }

            pszRest = StrChr(CharNext(pszRest), L'(');
        }

        if (!pszRest)
        {
            pszRest = PathFindExtension(pszTemplate);
            cchStem = static_cast<int>(pszRest - pszTemplate);

            hr = StringCchCopy(szFormat, ARRAYSIZE(szFormat), L" (%lu)");
        }
        else
        {
            pszRest++;

            cchStem = static_cast<int>(pszRest - pszTemplate);

            while (*pszRest && *pszRest >= L'0' && *pszRest <= L'9')
            {
                pszRest++;
            }

            hr = StringCchCopy(szFormat, ARRAYSIZE(szFormat), L"%lu");
        }
    }

    unsigned long ulMax = 0;
    unsigned long ulMin = 0;
    if (SUCCEEDED(hr))
    {
        int cchFormat = lstrlen(szFormat);
        if (cchFormat < 3)
        {
            *pszUniqueName = L'\0';
            return FALSE;
        }
        ulMin = ulMinLong;
        cchTmp = cchMax - cchDir - cchStem - (cchFormat - 3);
        switch (cchTmp)
        {
        case 1:
            ulMax = 10;
            break;
        case 2:
            ulMax = 100;
            break;
        case 3:
            ulMax = 1000;
            break;
        case 4:
            ulMax = 10000;
            break;
        case 5:
            ulMax = 100000;
            break;
        default:
            if (cchTmp <= 0)
            {
                ulMax = ulMin;
            }
            else
            {
                ulMax = 1000000;
            }
            break;
        }
    }

    if (SUCCEEDED(hr))
    {
        hr = StringCchCopyN(pszName, pszUniqueName + cchMax - pszName, pszStem, cchStem);
        if (SUCCEEDED(hr))
        {
            PWSTR pszDigit = pszName + cchStem;

            for (unsigned long ul = ulMin; ((ul < ulMax) && (!fRet)); ul++)
            {
                wchar_t szTemp[MAX_PATH] = { 0 };
                hr = StringCchPrintf(szTemp, ARRAYSIZE(szTemp), szFormat, ul);
                if (SUCCEEDED(hr))
                {
                    hr = StringCchCat(szTemp, ARRAYSIZE(szTemp), pszRest);
                    if (SUCCEEDED(hr))
                    {
                        hr = StringCchCopy(pszDigit, pszUniqueName + cchMax - pszDigit, szTemp);
                        if (SUCCEEDED(hr))
                        {
                            if (!PathFileExists(pszUniqueName))
                            {
                                (*pulNumUsed) = ul;
                                fRet = TRUE;
                            }
                        }
                    }
                }
            }
        }
    }

    if (!fRet)
    {
        *pszUniqueName = L'\0';
    }

    return fRet;
}

// Iterate through the shell items array and checks if at least 1 item has SFGAO_CANRENAME.
// We do not enumerate child items - only items the user selected.
bool ShellItemArrayContainsRenamableItem(_In_ IShellItemArray* shellItemArray)
{
    bool hasRenamable = false;
    IEnumShellItems* spesi;
    if (SUCCEEDED(shellItemArray->EnumItems(&spesi)))
    {
        ULONG celtFetched;
        IShellItem* spsi;
        while ((S_OK == spesi->Next(1, &spsi, &celtFetched)))
        {
            SFGAOF attrs;
            if (SUCCEEDED(spsi->GetAttributes(SFGAO_CANRENAME, &attrs)) &&
                attrs & SFGAO_CANRENAME)
            {
                hasRenamable = true;
                break;
            }
        }
    }

    return hasRenamable;
}

// Iterate through the data source and checks if at least 1 item has SFGAO_CANRENAME.
// We do not enumerate child items - only items the user selected.
bool DataObjectContainsRenamableItem(_In_ IUnknown* dataSource)
{
    bool hasRenamable = false;
    CComPtr<IShellItemArray> spsia;
    if (dataSource && SUCCEEDED(GetShellItemArrayFromDataObject(dataSource, &spsia)))
    {
        CComPtr<IEnumShellItems> spesi;
        if (SUCCEEDED(spsia->EnumItems(&spesi)))
        {
            ULONG celtFetched;
            CComPtr<IShellItem> spsi;
            while ((S_OK == spesi->Next(1, &spsi, &celtFetched)))
            {
                SFGAOF attrs;
                if (SUCCEEDED(spsi->GetAttributes(SFGAO_CANRENAME, &attrs)) &&
                    attrs & SFGAO_CANRENAME)
                {
                    hasRenamable = true;
                    break;
                }
            }
        }
    }
    return hasRenamable;
}

HWND CreateMsgWindow(_In_ HINSTANCE hInst, _In_ WNDPROC pfnWndProc, _In_ void* p)
{
    WNDCLASS wc = { 0 };
    PCWSTR wndClassName = L"MsgWindow";

    wc.lpfnWndProc = DefWindowProc;
    wc.cbWndExtra = sizeof(void*);
    wc.hInstance = hInst;
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_BTNFACE + 1);
    wc.lpszClassName = wndClassName;

    RegisterClass(&wc);

    HWND hwnd = CreateWindowEx(
        0, wndClassName, nullptr, 0, 0, 0, 0, 0, HWND_MESSAGE, 0, hInst, nullptr);
    if (hwnd)
    {
        SetWindowLongPtr(hwnd, 0, reinterpret_cast<LONG_PTR>(p));
        if (pfnWndProc)
        {
            SetWindowLongPtr(hwnd, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(pfnWndProc));
        }
    }

    return hwnd;
}

std::wstring GetRegString(const std::wstring& valueName, const std::wstring& subPath)
{
    wchar_t value[MAX_INPUT_STRING_LEN];
    value[0] = L'\0';
    DWORD type = REG_SZ;
    DWORD size = MAX_INPUT_STRING_LEN * sizeof(wchar_t);
    std::wstring completePath = std::wstring(c_rootRegPath) + subPath;
    if (SHGetValue(HKEY_CURRENT_USER, completePath.c_str(), valueName.c_str(), &type, value, &size) == ERROR_SUCCESS)
    {
        return std::wstring(value);
    }
    return std::wstring{};
}

unsigned int GetRegNumber(const std::wstring& valueName, unsigned int defaultValue)
{
    DWORD type = REG_DWORD;
    DWORD data = 0;
    DWORD size = sizeof(DWORD);
    if (SHGetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName.c_str(), &type, &data, &size) == ERROR_SUCCESS)
    {
        return data;
    }
    return defaultValue;
}

void SetRegNumber(const std::wstring& valueName, unsigned int value)
{
    SHSetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName.c_str(), REG_DWORD, &value, sizeof(value));
}

bool GetRegBoolean(const std::wstring& valueName, bool defaultValue)
{
    DWORD value = GetRegNumber(valueName.c_str(), defaultValue ? 1 : 0);
    return (value == 0) ? false : true;
}

void SetRegBoolean(const std::wstring& valueName, bool value)
{
    SetRegNumber(valueName, value ? 1 : 0);
}

bool LastModifiedTime(const std::wstring& filePath, FILETIME* lpFileTime)
{
    WIN32_FILE_ATTRIBUTE_DATA attr{};
    if (GetFileAttributesExW(filePath.c_str(), GetFileExInfoStandard, &attr))
    {
        *lpFileTime = attr.ftLastWriteTime;
        return true;
    }
    return false;
}

std::wstring CreateGuidStringWithoutBrackets()
{
    GUID guid;
    if (CoCreateGuid(&guid) == S_OK)
    {
        OLECHAR* guidString;
        if (StringFromCLSID(guid, &guidString) == S_OK)
        {
            std::wstring guidStr{ guidString };
            CoTaskMemFree(guidString);
            return guidStr.substr(1, guidStr.length() - 2);
        }
    }

    return L"";
}
