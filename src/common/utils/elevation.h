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

#include <string>
#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

namespace 
{
    inline std::wstring GetErrorString(HRESULT handle)
    {
         _com_error err(handle);
         return err.ErrorMessage();
    }

    inline bool FindDesktopFolderView(REFIID riid, void** ppv)
    {
        CComPtr<IShellWindows> spShellWindows;
        auto result = spShellWindows.CoCreateInstance(CLSID_ShellWindows);
        if (result != S_OK)
        {
            Logger::warn(L"Failed to create instance. {}", GetErrorString(result));
            return false;
        }

        CComVariant vtLoc(CSIDL_DESKTOP);
        CComVariant vtEmpty;
        long lhwnd;
        CComPtr<IDispatch> spdisp;
        result = spShellWindows->FindWindowSW(
            &vtLoc, &vtEmpty, SWC_DESKTOP, &lhwnd, SWFO_NEEDDISPATCH, &spdisp);

        if (result != S_OK)
        {
            Logger::warn(L"Failed to find the window. {}", GetErrorString(result));
            return false;
        }

        CComPtr<IShellBrowser> spBrowser;
        result = CComQIPtr<IServiceProvider>(spdisp)->QueryService(SID_STopLevelBrowser,
                                                          IID_PPV_ARGS(&spBrowser));
        if (result != S_OK)
        {
            Logger::warn(L"Failed to query service. {}", GetErrorString(result));
            return false;
        }

        CComPtr<IShellView> spView;
        result = spBrowser->QueryActiveShellView(&spView);
        if (result != S_OK)
        {
            Logger::warn(L"Failed to query active shell window. {}", GetErrorString(result));
            return false;
        }

        result = spView->QueryInterface(riid, ppv);
        if (result != S_OK)
        {
            Logger::warn(L"Failed to query interface. {}", GetErrorString(result));
            return false;
        }

        return true;
    }

    inline bool GetDesktopAutomationObject(REFIID riid, void** ppv)
    {
        CComPtr<IShellView> spsv;
        if (!FindDesktopFolderView(IID_PPV_ARGS(&spsv)))
        {
            return false;
        }

        CComPtr<IDispatch> spdispView;
        auto result = spsv->GetItemObject(SVGIO_BACKGROUND, IID_PPV_ARGS(&spdispView));
        if (result != S_OK)
        {
            Logger::warn(L"GetItemObject() failed. {}", GetErrorString(result));
            return false;
        }

        result = spdispView->QueryInterface(riid, ppv);
        if (result != S_OK)
        {
            Logger::warn(L"QueryInterface() failed. {}", GetErrorString(result));
            return false;
        }

        return true;
    }

    inline bool ShellExecuteFromExplorer(
        PCWSTR pszFile,
        PCWSTR pszParameters = nullptr)
    {
        CComPtr<IShellFolderViewDual> spFolderView;
        if (!GetDesktopAutomationObject(IID_PPV_ARGS(&spFolderView)))
        {
            return false;
        }

        CComPtr<IDispatch> spdispShell;
        auto result = spFolderView->get_Application(&spdispShell);
        if (result != S_OK)
        {
            Logger::warn(L"get_Application() failed. {}", GetErrorString(result));
            return false;
        }

        CComQIPtr<IShellDispatch2>(spdispShell)
            ->ShellExecute(CComBSTR(pszFile),
                           CComVariant(pszParameters ? pszParameters : L""),
                           CComVariant(L""),
                           CComVariant(L""),
                           CComVariant(SW_SHOWNORMAL));

        return true;
    }
}

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
    DWORD size = (DWORD)sizeof(TOKEN_MANDATORY_LABEL) + ::GetLengthSid(medium_sid);

    BOOL result = SetTokenInformation(token, TokenIntegrityLevel, &label, size);
    LocalFree(medium_sid);
    CloseHandle(token);

    return result;
}

// Run command as elevated user, returns true if succeeded
inline HANDLE run_elevated(const std::wstring& file, const std::wstring& params)
{
    Logger::info(L"run_elevated with params={}", params);
    SHELLEXECUTEINFOW exec_info = { 0 };
    exec_info.cbSize = sizeof(SHELLEXECUTEINFOW);
    exec_info.lpVerb = L"runas";
    exec_info.lpFile = file.c_str();
    exec_info.lpParameters = params.c_str();
    exec_info.hwnd = 0;
    exec_info.fMask = SEE_MASK_NOCLOSEPROCESS;
    exec_info.lpDirectory = 0;
    exec_info.hInstApp = 0;
    exec_info.nShow = SW_SHOWDEFAULT;

    return ShellExecuteExW(&exec_info) ? exec_info.hProcess : nullptr;
}

// Run command as non-elevated user, returns true if succeeded, puts the process id into returnPid if returnPid != NULL
inline bool run_non_elevated(const std::wstring& file, const std::wstring& params, DWORD* returnPid)
{
    Logger::info(L"run_non_elevated with params={}", params);
    auto executable_args = L"\"" + file + L"\"";
    if (!params.empty())
    {
        executable_args += L" " + params;
    }

    HWND hwnd = GetShellWindow();
    if (!hwnd)
    {
        if (GetLastError() == ERROR_SUCCESS)
        {
            Logger::warn(L"GetShellWindow() returned null. Shell window is not available");
        }
        else
        {
            Logger::error(L"GetShellWindow() failed. {}", get_last_error_or_default(GetLastError()));
        }
        
        return false;
    }
    DWORD pid;
    GetWindowThreadProcessId(hwnd, &pid);

    winrt::handle process{ OpenProcess(PROCESS_CREATE_PROCESS, FALSE, pid) };
    if (!process)
    {
        Logger::error(L"OpenProcess() failed. {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    SIZE_T size = 0;

    InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
    auto pproc_buffer = std::make_unique<char[]>(size);
    auto pptal = reinterpret_cast<PPROC_THREAD_ATTRIBUTE_LIST>(pproc_buffer.get());
    if (!pptal)
    {
        Logger::error(L"pptal failed to initialize. {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    if (!InitializeProcThreadAttributeList(pptal, 1, 0, &size))
    {
        Logger::error(L"InitializeProcThreadAttributeList() failed. {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    HANDLE process_handle = process.get();
    if (!UpdateProcThreadAttribute(pptal,
                                   0,
                                   PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                                   &process_handle,
                                   sizeof(process_handle),
                                   nullptr,
                                   nullptr))
    {
        Logger::error(L"UpdateProcThreadAttribute() failed. {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    STARTUPINFOEX siex = { 0 };
    siex.lpAttributeList = pptal;
    siex.StartupInfo.cb = sizeof(siex);

    PROCESS_INFORMATION pi = { 0 };
    auto succeeded = CreateProcessW(file.c_str(),
                                    const_cast<LPWSTR>(executable_args.c_str()),
                                    nullptr,
                                    nullptr,
                                    FALSE,
                                    EXTENDED_STARTUPINFO_PRESENT,
                                    nullptr,
                                    nullptr,
                                    &siex.StartupInfo,
                                    &pi);
    if (succeeded)
    {
        if (pi.hProcess)
        {
            if (returnPid)
            {
                *returnPid = GetProcessId(pi.hProcess);
            }

            CloseHandle(pi.hProcess);
        }
        if (pi.hThread)
        {
            CloseHandle(pi.hThread);
        }
    }
    else
    {
        Logger::error(L"CreateProcessW() failed. {}", get_last_error_or_default(GetLastError()));
    }

    return succeeded;
}

inline bool RunNonElevatedEx(const std::wstring& file, const std::wstring& params)
{
    try
    {
        CoInitialize(nullptr);
        if (!ShellExecuteFromExplorer(file.c_str(), params.c_str()))
        {
            return false;
        }
    }
    catch(...)
    {
        return false;
    }

    return true;
}

// Run command with the same elevation, returns true if succeeded
inline bool run_same_elevation(const std::wstring& file, const std::wstring& params, DWORD* returnPid)
{
    auto executable_args = L"\"" + file + L"\"";
    if (!params.empty())
    {
        executable_args += L" " + params;
    }

    STARTUPINFO si = { sizeof(STARTUPINFO) };
    PROCESS_INFORMATION pi = { 0 };
    auto succeeded = CreateProcessW(file.c_str(),
                                    const_cast<LPWSTR>(executable_args.c_str()),
                                    nullptr,
                                    nullptr,
                                    FALSE,
                                    0,
                                    nullptr,
                                    nullptr,
                                    &si,
                                    &pi);

    if (succeeded)
    {
        if (pi.hProcess)
        {
            if (returnPid)
            {
                *returnPid = GetProcessId(pi.hProcess);
            }

            CloseHandle(pi.hProcess);
        }

        if (pi.hThread)
        {
            CloseHandle(pi.hThread);
        }
    }
    return succeeded;
}

// Returns true if the current process is running from administrator account
// The function returns true in case of error since we want to return false
// only in case of a positive verification that the user is not an admin.
inline bool check_user_is_admin()
{
    auto freeMemory = [](PSID pSID, PTOKEN_GROUPS pGroupInfo) {
        if (pSID)
        {
            FreeSid(pSID);
        }
        if (pGroupInfo)
        {
            GlobalFree(pGroupInfo);
        }
    };

    HANDLE hToken;
    DWORD dwSize = 0;
    PTOKEN_GROUPS pGroupInfo;
    SID_IDENTIFIER_AUTHORITY SIDAuth = SECURITY_NT_AUTHORITY;
    PSID pSID = NULL;

    // Open a handle to the access token for the calling process.
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &hToken))
    {
        return true;
    }

    // Call GetTokenInformation to get the buffer size.
    if (!GetTokenInformation(hToken, TokenGroups, NULL, dwSize, &dwSize))
    {
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            return true;
        }
    }

    // Allocate the buffer.
    pGroupInfo = (PTOKEN_GROUPS)GlobalAlloc(GPTR, dwSize);

    // Call GetTokenInformation again to get the group information.
    if (!GetTokenInformation(hToken, TokenGroups, pGroupInfo, dwSize, &dwSize))
    {
        freeMemory(pSID, pGroupInfo);
        return true;
    }

    // Create a SID for the BUILTIN\Administrators group.
    if (!AllocateAndInitializeSid(&SIDAuth, 2, SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS, 0, 0, 0, 0, 0, 0, &pSID))
    {
        freeMemory(pSID, pGroupInfo);
        return true;
    }

    // Loop through the group SIDs looking for the administrator SID.
    for (DWORD i = 0; i < pGroupInfo->GroupCount; ++i)
    {
        if (EqualSid(pSID, pGroupInfo->Groups[i].Sid))
        {
            freeMemory(pSID, pGroupInfo);
            return true;
        }
    }

    freeMemory(pSID, pGroupInfo);
    return false;
}