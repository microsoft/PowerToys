#pragma once
#include <vector>
#include <string>

// Checks if a process path is included in a list of strings.
inline bool find_app_name_in_path(const std::wstring& where, const std::vector<std::wstring>& what)
{
    for (const auto& row : what)
    {
        const auto pos = where.rfind(row);
        const auto last_slash = where.rfind('\\');
        //Check that row occurs in where, and its last occurrence contains in itself the first character after the last backslash.
        if (pos != std::wstring::npos && pos <= last_slash + 1 && pos + row.length() > last_slash)
        {
            return true;
        }
    }
    return false;
}

inline bool find_folder_in_path(const std::wstring& where, const std::vector<std::wstring>& what)
{
    for (const auto& row : what)
    {
        const auto pos = where.rfind(row);
        if (pos != std::wstring::npos)
        {
            return true;
        }
    }
    return false;
}

#define MAX_TITLE_LENGTH 255
inline bool check_excluded_app_with_title(const HWND& hwnd, const std::vector<std::wstring>& excludedApps)
{
    WCHAR title[MAX_TITLE_LENGTH];
    int len = GetWindowTextW(hwnd, title, MAX_TITLE_LENGTH);
    if (len <= 0)
    {
        return false;
    }

    std::wstring titleStr(title);
    CharUpperBuffW(titleStr.data(), static_cast<DWORD>(titleStr.length()));

    for (const auto& app : excludedApps)
    {
        if (titleStr.contains(app))
        {
            return true;
        }
    }
    return false;
}

inline bool check_excluded_app(const HWND& hwnd, const std::wstring& processPath, const std::vector<std::wstring>& excludedApps)
{
    bool res = find_app_name_in_path(processPath, excludedApps);

    if (!res)
    {
        res = check_excluded_app_with_title(hwnd, excludedApps);
    }

    return res;
}
