#include "pch.h"
#include "elevation.h"

namespace
{
    std::wstring GetErrorString(HRESULT handle)
    {
        _com_error err(handle);
        return err.ErrorMessage();
    }

    bool FindDesktopFolderView(REFIID riid, void** ppv)
    {
        CComPtr<IShellWindows> spShellWindows;
        auto result = spShellWindows.CoCreateInstance(CLSID_ShellWindows);
        if (result != S_OK || spShellWindows == nullptr)
        {
            Logger::warn(L"Failed to create instance. {}", GetErrorString(result));
            return false;
        }

        CComVariant vtLoc(static_cast<long>(CSIDL_DESKTOP));
        CComVariant vtEmpty;
        long lhwnd;
        CComPtr<IDispatch> spdisp;
        result = spShellWindows->FindWindowSW(
            &vtLoc, &vtEmpty, SWC_DESKTOP, &lhwnd, SWFO_NEEDDISPATCH, &spdisp);

        if (result != S_OK || spdisp == nullptr)
        {
            Logger::warn(L"Failed to find the window. {}", GetErrorString(result));
            return false;
        }

        CComPtr<IShellBrowser> spBrowser;
        result = CComQIPtr<IServiceProvider>(spdisp)->QueryService(SID_STopLevelBrowser,
                                                                   IID_PPV_ARGS(&spBrowser));
        if (result != S_OK || spBrowser == nullptr)
        {
            Logger::warn(L"Failed to query service. {}", GetErrorString(result));
            return false;
        }

        CComPtr<IShellView> spView;
        result = spBrowser->QueryActiveShellView(&spView);
        if (result != S_OK || spView == nullptr)
        {
            Logger::warn(L"Failed to query active shell window. {}", GetErrorString(result));
            return false;
        }

        result = spView->QueryInterface(riid, ppv);
        if (result != S_OK || ppv == nullptr || *ppv == nullptr)
        {
            Logger::warn(L"Failed to query interface. {}", GetErrorString(result));
            return false;
        }

        return true;
    }

    bool GetDesktopAutomationObject(REFIID riid, void** ppv)
    {
        CComPtr<IShellView> spsv;

        // Desktop may not be available on startup
        auto attempts = 5;
        for (auto i = 1; i <= attempts; i++)
        {
            if (FindDesktopFolderView(IID_PPV_ARGS(&spsv)))
            {
                break;
            }

            Logger::warn(L"FindDesktopFolderView() failed attempt {}", i);

            if (i == attempts)
            {
                Logger::warn(L"FindDesktopFolderView() max attempts reached");
                return false;
            }
            Sleep(100);
        }

        CComPtr<IDispatch> spdispView;
        HRESULT result = spsv->GetItemObject(SVGIO_BACKGROUND, IID_PPV_ARGS(&spdispView));
        if (result != S_OK || spdispView == nullptr)
        {
            Logger::warn(L"spsv->GetItemObject() failed. {}", GetErrorString(result));
            return false;
        }

        return SUCCEEDED(spdispView->QueryInterface(riid, ppv));
    }

    bool ShellExecuteFromExplorer(LPCWSTR file, LPCWSTR parameters, LPCWSTR directory)
    {
        CComPtr<IShellFolderViewDual> spFolderView;
        if (!GetDesktopAutomationObject(IID_PPV_ARGS(&spFolderView)))
        {
            Logger::warn(L"GetDesktopAutomationObject() failed.");
            return false;
        }

        CComPtr<IDispatch> spdispShell;
        auto result = spFolderView->get_Application(&spdispShell);
        if (result != S_OK || spdispShell == nullptr)
        {
            Logger::warn(L"spFolderView->get_Application() failed. {}", GetErrorString(result));
            return false;
        }

        CComQIPtr<IShellDispatch2> shell(spdispShell);
        if (shell == nullptr)
        {
            Logger::warn(L"IShellDispatch2 is nullptr");
            return false;
        }

        CComVariant args(parameters);
        CComVariant dir(directory);
        CComVariant operation(L"open");
        CComVariant show(SW_SHOWNORMAL);
        result = shell->ShellExecute(CComBSTR(file), args, dir, operation, show);
        if (result != S_OK)
        {
            Logger::warn(L"IShellDispatch2::ShellExecute() failed. {}", GetErrorString(result));
            return false;
        }

        return true;
    }
}

bool is_process_elevated(const bool use_cached_value)
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

bool drop_elevated_privileges()
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

HANDLE run_as_different_user(const std::wstring& file, const std::wstring& params, const wchar_t* workingDir, const bool showWindow)
{
    Logger::info(L"run_elevated with params={}", params);
    SHELLEXECUTEINFOW exec_info = { 0 };
    exec_info.cbSize = sizeof(SHELLEXECUTEINFOW);
    exec_info.lpVerb = L"runAsUser";
    exec_info.lpFile = file.c_str();
    exec_info.lpParameters = params.c_str();
    exec_info.hwnd = 0;
    exec_info.fMask = SEE_MASK_NOCLOSEPROCESS;
    exec_info.lpDirectory = workingDir;
    exec_info.hInstApp = 0;
    if (showWindow)
    {
        exec_info.nShow = SW_SHOWDEFAULT;
    }
    else
    {
        exec_info.nShow = SW_HIDE;
    }

    return ShellExecuteExW(&exec_info) ? exec_info.hProcess : nullptr;
}

HANDLE run_elevated(const std::wstring& file, const std::wstring& params, const wchar_t* workingDir, const bool showWindow)
{
    Logger::info(L"run_elevated with params={}", params);
    SHELLEXECUTEINFOW exec_info = { 0 };
    exec_info.cbSize = sizeof(SHELLEXECUTEINFOW);
    exec_info.lpVerb = L"runas";
    exec_info.lpFile = file.c_str();
    exec_info.lpParameters = params.c_str();
    exec_info.hwnd = 0;
    exec_info.fMask = SEE_MASK_NOCLOSEPROCESS;
    exec_info.lpDirectory = workingDir;
    exec_info.hInstApp = 0;

    if (showWindow)
    {
        exec_info.nShow = SW_SHOWDEFAULT;
    }
    else
    {
        exec_info.nShow = SW_HIDE;
    }

    BOOL result = ShellExecuteExW(&exec_info);

    return result ? exec_info.hProcess : nullptr;
}

bool run_non_elevated(const std::wstring& file, const std::wstring& params, DWORD* returnPid, const wchar_t* workingDir, const bool showWindow)
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
    auto dwCreationFlags = EXTENDED_STARTUPINFO_PRESENT;

    if (!showWindow)
    {
        siex.StartupInfo.dwFlags = STARTF_USESHOWWINDOW;
        siex.StartupInfo.wShowWindow = SW_HIDE;
        dwCreationFlags = CREATE_NO_WINDOW;
    }

    auto succeeded = CreateProcessW(file.c_str(),
                                    &executable_args[0],
                                    nullptr,
                                    nullptr,
                                    FALSE,
                                    dwCreationFlags,
                                    nullptr,
                                    workingDir,
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

bool RunNonElevatedEx(const std::wstring& file, const std::wstring& params, const std::wstring& working_dir)
{
    bool success = false;
    HRESULT co_init = E_FAIL;
    try
    {
        co_init = CoInitialize(nullptr);
        success = ShellExecuteFromExplorer(file.c_str(), params.c_str(), working_dir.c_str());
    }
    catch (...)
    {
    }
    if (SUCCEEDED(co_init))
    {
        CoUninitialize();
    }

    return success;
}

std::optional<ProcessInfo> RunNonElevatedFailsafe(const std::wstring& file, const std::wstring& params, const std::wstring& working_dir, DWORD handleAccess)
{
    bool launched = RunNonElevatedEx(file, params, working_dir);
    if (!launched)
    {
        Logger::warn(L"RunNonElevatedEx() failed. Trying fallback");
        std::wstring action_runner_path = get_module_folderpath() + L"\\PowerToys.ActionRunner.exe";
        std::wstring newParams = fmt::format(L"-run-non-elevated -target \"{}\" {}", file, params);
        launched = run_non_elevated(action_runner_path, newParams, nullptr, working_dir.c_str());
        if (launched)
        {
            Logger::trace(L"Started {}", file);
        }
        else
        {
            Logger::warn(L"Failed to start {}", file);
            return std::nullopt;
        }
    }

    auto handles = getProcessHandlesByName(std::filesystem::path{ file }.filename().wstring(), PROCESS_QUERY_INFORMATION | SYNCHRONIZE | handleAccess);

    if (handles.empty())
        return std::nullopt;

    ProcessInfo result;
    result.processID = GetProcessId(handles[0].get());
    result.processHandle = std::move(handles[0]);

    return result;
}

bool run_same_elevation(const std::wstring& file, const std::wstring& params, DWORD* returnPid, const wchar_t* workingDir)
{
    auto executable_args = L"\"" + file + L"\"";
    if (!params.empty())
    {
        executable_args += L" " + params;
    }

    STARTUPINFO si = { sizeof(STARTUPINFO) };
    PROCESS_INFORMATION pi = { 0 };

    auto succeeded = CreateProcessW(file.c_str(),
                                    &executable_args[0],
                                    nullptr,
                                    nullptr,
                                    FALSE,
                                    0,
                                    nullptr,
                                    workingDir,
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

bool check_user_is_admin()
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

    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &hToken))
    {
        return true;
    }

    if (!GetTokenInformation(hToken, TokenGroups, NULL, dwSize, &dwSize))
    {
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            return true;
        }
    }

    pGroupInfo = static_cast<PTOKEN_GROUPS>(GlobalAlloc(GPTR, dwSize));

    if (!GetTokenInformation(hToken, TokenGroups, pGroupInfo, dwSize, &dwSize))
    {
        freeMemory(pSID, pGroupInfo);
        return true;
    }

    if (!AllocateAndInitializeSid(&SIDAuth, 2, SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS, 0, 0, 0, 0, 0, 0, &pSID))
    {
        freeMemory(pSID, pGroupInfo);
        return true;
    }

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

bool IsProcessOfWindowElevated(HWND window)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(window, &pid);
    if (!pid)
    {
        return false;
    }

    wil::unique_handle hProcess{ OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,
                                             FALSE,
                                             pid) };

    wil::unique_handle token;

    if (OpenProcessToken(hProcess.get(), TOKEN_QUERY, &token))
    {
        TOKEN_ELEVATION elevation;
        DWORD size;
        if (GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
        {
            return elevation.TokenIsElevated != 0;
        }
    }
    return false;
}
