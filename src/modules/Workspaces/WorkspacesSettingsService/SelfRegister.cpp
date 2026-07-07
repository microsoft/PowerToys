// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "SelfRegister.h"
#include "FileGuard.h"
#include "Paths.h"

#include <windows.h>
#include <vector>

namespace PTSettingsSvc
{
    namespace
    {
        std::wstring ServiceKeyName(const std::wstring& sid)
        {
            return L"PTSettingsSvc_" + sid;
        }

        std::wstring ServiceAccountName(const std::wstring& sid)
        {
            // Virtual service account derived from the service key name.  Passing
            // this as lpServiceStartName with a null password to CreateService
            // auto-creates the managed account NT SERVICE\PTSettingsSvc_<SID>.
            return L"NT SERVICE\\" + ServiceKeyName(sid);
        }

        std::wstring ServiceDisplayName(const std::wstring& sid)
        {
            // The ugly SID stays in the key; the display string is friendlier.
            return L"PowerToys Settings Service (" + sid + L")";
        }

        std::wstring OwnExePath()
        {
            wchar_t buf[MAX_PATH * 2] = {};
            DWORD n = GetModuleFileNameW(nullptr, buf, ARRAYSIZE(buf));
            if (n == 0 || n >= ARRAYSIZE(buf))
            {
                return {};
            }
            return std::wstring(buf, n);
        }

        // binPath = "<exe>" "<sid>"  — the SID flows back to the running service
        // as argv[1] (see wmain), which uses it as the pipe/owner SID.
        std::wstring BuildBinPath(const std::wstring& exePath, const std::wstring& sid)
        {
            return L"\"" + exePath + L"\" \"" + sid + L"\"";
        }

        void SetFailureActions(SC_HANDLE svc)
        {
            // Restart twice at 60s, reset the failure count after a day.
            SC_ACTION actions[2] = {};
            actions[0].Type = SC_ACTION_RESTART;
            actions[0].Delay = 60000;
            actions[1].Type = SC_ACTION_RESTART;
            actions[1].Delay = 60000;

            SERVICE_FAILURE_ACTIONSW fa{};
            fa.dwResetPeriod = 86400;
            fa.lpRebootMsg = nullptr;
            fa.lpCommand = nullptr;
            fa.cActions = ARRAYSIZE(actions);
            fa.lpsaActions = actions;
            ChangeServiceConfig2W(svc, SERVICE_CONFIG_FAILURE_ACTIONS, &fa);
        }

        bool StopAndWait(SC_HANDLE svc)
        {
            SERVICE_STATUS ss{};
            if (!ControlService(svc, SERVICE_CONTROL_STOP, &ss))
            {
                DWORD err = GetLastError();
                // Not running is fine.
                return err == ERROR_SERVICE_NOT_ACTIVE;
            }
            for (int i = 0; i < 20; ++i)
            {
                if (!QueryServiceStatus(svc, &ss) || ss.dwCurrentState == SERVICE_STOPPED)
                {
                    return true;
                }
                Sleep(250);
            }
            return false;
        }

        int RegisterInternal(const std::wstring& sid, bool startAfter)
        {
            const std::wstring exePath = OwnExePath();
            if (exePath.empty())
            {
                return static_cast<int>(GetLastError());
            }

            const std::wstring keyName = ServiceKeyName(sid);
            const std::wstring account = ServiceAccountName(sid);
            const std::wstring binPath = BuildBinPath(exePath, sid);

            SC_HANDLE scm = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CREATE_SERVICE);
            if (!scm)
            {
                return static_cast<int>(GetLastError());
            }

            int rc = 0;
            SC_HANDLE svc = CreateServiceW(
                scm,
                keyName.c_str(),
                ServiceDisplayName(sid).c_str(),
                SERVICE_ALL_ACCESS,
                SERVICE_WIN32_OWN_PROCESS,
                SERVICE_AUTO_START,
                SERVICE_ERROR_NORMAL,
                binPath.c_str(),
                nullptr,               // no load-order group
                nullptr,               // no tag
                nullptr,               // no dependencies
                account.c_str(),       // NT SERVICE\PTSettingsSvc_<SID> (virtual acct)
                nullptr);              // null password for a virtual account

            if (!svc)
            {
                DWORD err = GetLastError();
                if (err != ERROR_SERVICE_EXISTS)
                {
                    CloseServiceHandle(scm);
                    return static_cast<int>(err);
                }

                // Already exists (re-install / upgrade): re-point the binPath to
                // this (possibly new versioned) exe.  Stop first so the file
                // isn't held open, then reconfigure.
                svc = OpenServiceW(scm, keyName.c_str(),
                                   SERVICE_CHANGE_CONFIG | SERVICE_STOP |
                                       SERVICE_START | SERVICE_QUERY_STATUS);
                if (!svc)
                {
                    DWORD oerr = GetLastError();
                    CloseServiceHandle(scm);
                    return static_cast<int>(oerr);
                }

                StopAndWait(svc);

                if (!ChangeServiceConfigW(svc,
                                          SERVICE_WIN32_OWN_PROCESS,
                                          SERVICE_AUTO_START,
                                          SERVICE_ERROR_NORMAL,
                                          binPath.c_str(),
                                          nullptr, nullptr, nullptr,
                                          account.c_str(), nullptr,
                                          nullptr))
                {
                    rc = static_cast<int>(GetLastError());
                    CloseServiceHandle(svc);
                    CloseServiceHandle(scm);
                    return rc;
                }
            }

            SetFailureActions(svc);

            // Provision the protected store now that the virtual account exists.
            // Idempotent: re-asserts owner=SYSTEM + the protected DACL each run.
            HRESULT hr = ProvisionStore(GetSettingsRoot(),
                                        GetUserFolder(sid),
                                        sid,
                                        account);
            if (FAILED(hr))
            {
                CloseServiceHandle(svc);
                CloseServiceHandle(scm);
                return static_cast<int>(hr);
            }

            if (startAfter)
            {
                if (!StartServiceW(svc, 0, nullptr))
                {
                    DWORD serr = GetLastError();
                    if (serr != ERROR_SERVICE_ALREADY_RUNNING)
                    {
                        rc = static_cast<int>(serr);
                    }
                }
            }

            CloseServiceHandle(svc);
            CloseServiceHandle(scm);
            return rc;
        }
    }

    int RunRegister(const std::wstring& userSidString)
    {
        if (userSidString.empty())
        {
            return static_cast<int>(E_INVALIDARG);
        }
        return RegisterInternal(userSidString, /*startAfter*/ true);
    }

    int RunRepoint(const std::wstring& userSidString)
    {
        // Re-point is just an idempotent register against the existing service.
        return RunRegister(userSidString);
    }

    int RunUnregister(const std::wstring& userSidString)
    {
        if (userSidString.empty())
        {
            return static_cast<int>(E_INVALIDARG);
        }

        const std::wstring keyName = ServiceKeyName(userSidString);

        SC_HANDLE scm = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT);
        if (!scm)
        {
            return static_cast<int>(GetLastError());
        }

        SC_HANDLE svc = OpenServiceW(scm, keyName.c_str(),
                                     SERVICE_STOP | SERVICE_QUERY_STATUS | DELETE);
        if (!svc)
        {
            DWORD err = GetLastError();
            CloseServiceHandle(scm);
            // Already gone is success.
            return err == ERROR_SERVICE_DOES_NOT_EXIST ? 0 : static_cast<int>(err);
        }

        StopAndWait(svc);

        int rc = 0;
        if (!DeleteService(svc))
        {
            DWORD err = GetLastError();
            if (err != ERROR_SERVICE_MARKED_FOR_DELETE)
            {
                rc = static_cast<int>(err);
            }
        }

        CloseServiceHandle(svc);
        CloseServiceHandle(scm);
        return rc;
    }
}
