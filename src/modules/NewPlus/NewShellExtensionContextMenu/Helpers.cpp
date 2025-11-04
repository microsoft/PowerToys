// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "Helpers.h"
#include <regex>

// Minimal subset of PowerRename Helpers used by NewPlus
// This is a copy from PowerRename main branch to avoid cross-module dependencies

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
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$YY"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", (fileTime.wYear % 10));
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$Y"), replaceTerm);

        GetDateFormatEx(localeName, NULL, &fileTime, L"MMMM", formattedDate, MAX_PATH, NULL);
        formattedDate[0] = towupper(formattedDate[0]);
        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", formattedDate);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$MMMM"), replaceTerm);

        GetDateFormatEx(localeName, NULL, &fileTime, L"MMM", formattedDate, MAX_PATH, NULL);
        formattedDate[0] = towupper(formattedDate[0]);
        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", formattedDate);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$MMM"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wMonth);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$MM"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wMonth);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$M"), replaceTerm);

        GetDateFormatEx(localeName, NULL, &fileTime, L"dddd", formattedDate, MAX_PATH, NULL);
        formattedDate[0] = towupper(formattedDate[0]);
        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", formattedDate);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$DDDD"), replaceTerm);

        GetDateFormatEx(localeName, NULL, &fileTime, L"ddd", formattedDate, MAX_PATH, NULL);
        formattedDate[0] = towupper(formattedDate[0]);
        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", formattedDate);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$DDD"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wDay);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$DD"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wDay);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$D"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", hour12);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$HH"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", hour12);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$H"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", (fileTime.wHour < 12) ? L"AM" : L"PM");
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$TT"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%s"), L"$01", (fileTime.wHour < 12) ? L"am" : L"pm");
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$tt"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wHour);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$hh"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wHour);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$h"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wMinute);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$mm"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wMinute);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$m"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wSecond);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$ss"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wSecond);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$s"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%03d"), L"$01", fileTime.wMilliseconds);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$fff"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%02d"), L"$01", fileTime.wMilliseconds / 10);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$ff"), replaceTerm);

        StringCchPrintf(replaceTerm, MAX_PATH, TEXT("%s%d"), L"$01", fileTime.wMilliseconds / 100);
        res = regex_replace(res, std::wregex(L"(([^\\$]|^)(\\$\\$)*)\\$f"), replaceTerm);

        hr = StringCchCopy(result, cchMax, res.c_str());
    }

    return hr;
}
