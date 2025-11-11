#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <shellapi.h>
#include <sddl.h>
#include <shldisp.h>
#include <shlobj.h>
#include <exdisp.h>
#include <atlbase.h>
#include <stdlib.h>
#include <comdef.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.Collections.h>

#include <optional>

#include <string>
#include <filesystem>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>
#include <common/utils/process_path.h>
#include <common/utils/processApi.h>

// Returns true if the current process is running with elevated privileges
bool is_process_elevated(const bool use_cached_value = true);

// Drops the elevated privileges if present
bool drop_elevated_privileges();

// Run command as different user, returns process handle or null on failure
HANDLE run_as_different_user(const std::wstring& file, const std::wstring& params, const wchar_t* workingDir = nullptr, const bool showWindow = true);

// Run command as elevated user, returns process handle or null on failure
HANDLE run_elevated(const std::wstring& file, const std::wstring& params, const wchar_t* workingDir = nullptr, const bool showWindow = true);

// Run command as non-elevated user, returns true if succeeded, puts the process id into returnPid if returnPid != NULL
bool run_non_elevated(const std::wstring& file, const std::wstring& params, DWORD* returnPid, const wchar_t* workingDir = nullptr, const bool showWindow = true);

// Try running through the shell's automation object
bool RunNonElevatedEx(const std::wstring& file, const std::wstring& params, const std::wstring& working_dir);

struct ProcessInfo
{
    wil::unique_process_handle processHandle;
    DWORD processID = {};
};

// Fallback to ActionRunner when shell route fails and return process info if available
std::optional<ProcessInfo> RunNonElevatedFailsafe(const std::wstring& file, const std::wstring& params, const std::wstring& working_dir, DWORD handleAccess = 0);

// Run command with the same elevation, returns true if succeeded
bool run_same_elevation(const std::wstring& file, const std::wstring& params, DWORD* returnPid, const wchar_t* workingDir = nullptr);

// Returns true if the current process is running from administrator account
// The function returns true in case of error since we want to return false
// only in case of a positive verification that the user is not an admin.
bool check_user_is_admin();

bool IsProcessOfWindowElevated(HWND window);

