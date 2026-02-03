#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <shellapi.h>
#include <sddl.h>

#include <winrt/base.h>

#include <string>
#include <filesystem>
#include <optional>

#pragma warning(push)
#pragma warning(disable : 26471 26492 26493 26497)
#include <wil/resource.h>
#pragma warning(pop)

// Forward declarations - implementations in elevation.cpp
bool FindDesktopFolderView(REFIID riid, void** ppv);
bool GetDesktopAutomationObject(REFIID riid, void** ppv);
bool ShellExecuteFromExplorer(
    PCWSTR pszFile,
    PCWSTR pszParameters = nullptr,
    PCWSTR workingDir = L"");

// Returns true if the current process is running with elevated privileges
inline bool is_process_elevated(const bool use_cached_value = true)
{
    auto detection_func = []() {
        HANDLE token = nullptr;
        bool elevated = false;

        if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        {
            TOKEN_ELEVATION elevation;
            DWORD size;
            if (GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size))
            {
                elevated = (elevation.TokenIsElevated != 0);
            }
        }

        if (token)
        {
            CloseHandle(token);
        }

        return elevated;
    };
    static const bool cached_value = detection_func();
    return use_cached_value ? cached_value : detection_func();
}

// Drops the elevated privileges if present
inline bool drop_elevated_privileges()
{
    HANDLE token = nullptr;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_DEFAULT | WRITE_OWNER, &token))
    {
        return false;
    }

    PSID medium_sid = NULL;
    if (!::ConvertStringSidToSid(SDDL_ML_MEDIUM, &medium_sid))
    {
        return false;
    }

    TOKEN_MANDATORY_LABEL label = { 0 };
    label.Label.Attributes = SE_GROUP_INTEGRITY;
    label.Label.Sid = medium_sid;
    DWORD size = static_cast<DWORD>(sizeof(TOKEN_MANDATORY_LABEL) + ::GetLengthSid(medium_sid));

    BOOL result = SetTokenInformation(token, TokenIntegrityLevel, &label, size);
    LocalFree(medium_sid);
    CloseHandle(token);

    return result;
}

// Run command as different user, returns process handle if succeeded
HANDLE run_as_different_user(const std::wstring& file, const std::wstring& params, const wchar_t* workingDir = nullptr, const bool showWindow = true);

// Run command as elevated user, returns process handle if succeeded
HANDLE run_elevated(const std::wstring& file, const std::wstring& params, const wchar_t* workingDir = nullptr, const bool showWindow = true);

// Run command as non-elevated user, returns true if succeeded, puts the process id into returnPid if returnPid != NULL
bool run_non_elevated(const std::wstring& file, const std::wstring& params, DWORD* returnPid, const wchar_t* workingDir = nullptr, const bool showWindow = true);

bool RunNonElevatedEx(const std::wstring& file, const std::wstring& params, const std::wstring& working_dir);

struct ProcessInfo
{
    wil::unique_process_handle processHandle;
    DWORD processID = {};
};

std::optional<ProcessInfo> RunNonElevatedFailsafe(const std::wstring& file, const std::wstring& params, const std::wstring& working_dir, DWORD handleAccess = 0);

// Run command with the same elevation, returns true if succeeded
bool run_same_elevation(const std::wstring& file, const std::wstring& params, DWORD* returnPid, const wchar_t* workingDir = nullptr);

// Returns true if the current process is running from administrator account
// The function returns true in case of error since we want to return false
// only in case of a positive verification that the user is not an admin.
bool check_user_is_admin();

bool IsProcessOfWindowElevated(HWND window);
