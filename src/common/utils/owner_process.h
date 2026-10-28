// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>
#include <sddl.h>
#include <userenv.h>
#include <wil/resource.h>

#include <filesystem>
#include <string>
#include <system_error>
#include <vector>

#pragma comment(lib, "userenv.lib")
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "user32.lib")

namespace owner_process
{
    struct Identity
    {
        std::wstring sid;
        DWORD session = 0;
        DWORD integrity = 0;
        bool elevated = false;
    };

    inline bool IsNormalIdentity(const Identity& identity)
    {
        return !identity.elevated && identity.session != 0 &&
               identity.integrity == SECURITY_MANDATORY_MEDIUM_RID &&
               (identity.sid.starts_with(L"S-1-5-21-") || identity.sid.starts_with(L"S-1-12-1-"));
    }

    inline bool CanUseOwnerShell(const Identity& caller, const Identity& shell)
    {
        const bool allowedCaller = IsNormalIdentity(caller) ||
                                   (caller.elevated && caller.integrity >= SECURITY_MANDATORY_HIGH_RID);
        return allowedCaller && IsNormalIdentity(shell) && caller.sid == shell.sid && caller.session == shell.session;
    }

    inline std::vector<BYTE> TokenData(HANDLE token, TOKEN_INFORMATION_CLASS kind)
    {
        DWORD size = 0;
        GetTokenInformation(token, kind, nullptr, 0, &size);
        if (size == 0)
        {
            const auto error = GetLastError();
            throw std::system_error(error ? error : ERROR_INVALID_DATA, std::system_category(), "Get owner token data size");
        }
        std::vector<BYTE> bytes(size);
        if (!GetTokenInformation(token, kind, bytes.data(), size, &size))
        {
            throw std::system_error(GetLastError(), std::system_category(), "Read owner token data");
        }
        return bytes;
    }

    inline Identity ReadIdentity(HANDLE token)
    {
        Identity result;
        auto user = TokenData(token, TokenUser);
        wil::unique_hlocal_string sid;
        if (!ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(user.data())->User.Sid, sid.put()))
        {
            throw std::system_error(GetLastError(), std::system_category(), "Format owner SID");
        }
        result.sid = sid.get();
        DWORD size = 0;
        TOKEN_ELEVATION elevation{};
        if (!GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size) ||
            !GetTokenInformation(token, TokenSessionId, &result.session, sizeof(result.session), &size))
        {
            throw std::system_error(GetLastError(), std::system_category(), "Read owner session and elevation");
        }
        result.elevated = elevation.TokenIsElevated != FALSE;
        auto level = TokenData(token, TokenIntegrityLevel);
        PSID integrity = reinterpret_cast<TOKEN_MANDATORY_LABEL*>(level.data())->Label.Sid;
        result.integrity = *GetSidSubAuthority(integrity, *GetSidSubAuthorityCount(integrity) - 1);
        return result;
    }

    // This is an explicit process-launch boundary, not an authentication
    // fallback. The shell must be the same owner and Windows session.
    inline DWORD Start(const std::filesystem::path& image,
                       const std::wstring& parameters,
                       const std::filesystem::path& workingDirectory,
                       wil::unique_handle& process)
    {
        process.reset();
        try
        {
            if (!image.is_absolute() || !workingDirectory.is_absolute())
            {
                return ERROR_BAD_PATHNAME;
            }
            wil::unique_handle callerToken;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, callerToken.put()))
            {
                return GetLastError();
            }
            const auto caller = ReadIdentity(callerToken.get());
            if (caller.session == 0 ||
                (!caller.sid.starts_with(L"S-1-5-21-") && !caller.sid.starts_with(L"S-1-12-1-")))
            {
                return ERROR_INVALID_OWNER;
            }
            wil::unique_handle shellProcess;
            wil::unique_handle shellToken;
            wil::unique_handle normalPrimaryToken;
            STARTUPINFOW startup{};
            wchar_t desktopName[] = L"winsta0\\default";
            startup.cb = sizeof(startup);
            startup.dwFlags = STARTF_USESHOWWINDOW;
            startup.wShowWindow = SW_SHOWNORMAL;
            DWORD flags = CREATE_SUSPENDED;
            void* environment = nullptr;
            const auto releaseEnvironment = wil::scope_exit([&] {
                if (environment)
                {
                    DestroyEnvironmentBlock(environment);
                }
            });
            if (!IsNormalIdentity(caller))
            {
                if (!caller.elevated || caller.integrity < SECURITY_MANDATORY_HIGH_RID)
                {
                    return ERROR_ACCESS_DENIED;
                }
                const HWND desktop = GetShellWindow();
                DWORD shellPid = 0;
                if (!desktop || !GetWindowThreadProcessId(desktop, &shellPid))
                {
                    return ERROR_NO_SUCH_LOGON_SESSION;
                }
                shellProcess.reset(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, shellPid));
                if (!shellProcess || WaitForSingleObject(shellProcess.get(), 0) != WAIT_TIMEOUT ||
                    !OpenProcessToken(shellProcess.get(), TOKEN_QUERY | TOKEN_DUPLICATE, shellToken.put()))
                {
                    return ERROR_ACCESS_DENIED;
                }
                if (!CanUseOwnerShell(caller, ReadIdentity(shellToken.get())))
                {
                    return ERROR_INVALID_OWNER;
                }
                TOKEN_TYPE tokenType{};
                DWORD returned = 0;
                if (!GetTokenInformation(shellToken.get(), TokenType, &tokenType, sizeof(tokenType), &returned) || tokenType != TokenPrimary)
                {
                    return ERROR_BAD_TOKEN_TYPE;
                }
                std::vector<wchar_t> shellImage(32768);
                DWORD imageLength = static_cast<DWORD>(shellImage.size());
                wchar_t windowsDirectory[MAX_PATH + 1]{};
                const DWORD windowsLength = GetWindowsDirectoryW(windowsDirectory, ARRAYSIZE(windowsDirectory));
                if (!QueryFullProcessImageNameW(shellProcess.get(), 0, shellImage.data(), &imageLength) ||
                    windowsLength == 0 || windowsLength >= ARRAYSIZE(windowsDirectory) ||
                    _wcsicmp(shellImage.data(), (std::filesystem::path(windowsDirectory) / L"explorer.exe").c_str()) != 0)
                {
                    return ERROR_INVALID_OWNER;
                }
                if (!DuplicateTokenEx(shellToken.get(), MAXIMUM_ALLOWED, nullptr, SecurityImpersonation, TokenPrimary, normalPrimaryToken.put()) ||
                    !CreateEnvironmentBlock(&environment, normalPrimaryToken.get(), FALSE))
                {
                    return GetLastError();
                }
                startup.lpDesktop = desktopName;
                flags |= CREATE_UNICODE_ENVIRONMENT;
            }
            std::wstring command = L"\"" + image.wstring() + L"\"";
            if (!parameters.empty())
            {
                command += L" " + parameters;
            }
            PROCESS_INFORMATION child{};
            const BOOL created = normalPrimaryToken ?
                CreateProcessWithTokenW(normalPrimaryToken.get(), 0, image.c_str(), command.data(), flags,
                                        environment, workingDirectory.c_str(), &startup, &child) :
                CreateProcessW(image.c_str(), command.data(), nullptr, nullptr, FALSE, flags, environment,
                               workingDirectory.c_str(), &startup, &child);
            if (!created)
            {
                return GetLastError();
            }
            wil::unique_handle childProcess(child.hProcess);
            wil::unique_handle childThread(child.hThread);
            bool resumed = false;
            const auto rejectSuspendedChild = wil::scope_exit([&] {
                if (!resumed)
                {
                    TerminateProcess(childProcess.get(), ERROR_ACCESS_DENIED);
                    WaitForSingleObject(childProcess.get(), 10000);
                }
            });
            wil::unique_handle childToken;
            if (!OpenProcessToken(childProcess.get(), TOKEN_QUERY, childToken.put()) ||
                !CanUseOwnerShell(caller, ReadIdentity(childToken.get())))
            {
                return ERROR_INVALID_OWNER;
            }
            if (ResumeThread(childThread.get()) == MAXDWORD)
            {
                return GetLastError();
            }
            resumed = true;
            process = std::move(childProcess);
            return ERROR_SUCCESS;
        }
        catch (const std::system_error& error)
        {
            return error.code().value() ? static_cast<DWORD>(error.code().value()) : ERROR_GEN_FAILURE;
        }
        catch (const std::bad_alloc&)
        {
            return ERROR_NOT_ENOUGH_MEMORY;
        }
    }
}
