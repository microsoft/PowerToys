#pragma once

#include <vector>
#include <string>
#include <windows.h>

// Checks if a process path is included in a list of strings.
bool find_app_name_in_path(const std::wstring& where, const std::vector<std::wstring>& what);

bool find_folder_in_path(const std::wstring& where, const std::vector<std::wstring>& what);

#define MAX_TITLE_LENGTH 255
bool check_excluded_app_with_title(const HWND& hwnd, const std::vector<std::wstring>& excludedApps);

bool check_excluded_app(const HWND& hwnd, const std::wstring& processPath, const std::vector<std::wstring>& excludedApps);
