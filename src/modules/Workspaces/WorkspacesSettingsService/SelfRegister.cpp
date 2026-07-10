// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "SelfRegister.h"
#include "FileGuard.h"
#include "Paths.h"
#include "CallerVerify.h"

#include <windows.h>
#include <vector>
#include <string>
#include <fstream>
#include <chrono>

namespace PTSettingsSvc
{
    namespace
    {
        constexpr const wchar_t* kExeName = L"PowerToys.PTSettingsSvc.exe";

        // Append a timed line to the register step-timing log next to the bin root
        // (elevated context can write %ProgramData%).  Best-effort; never throws.
        void RegLog(const std::wstring& msg)
        {
            try
            {
                CreateDirectoryW(GetServiceBinRoot().c_str(), nullptr);
                std::wstring path = GetServiceBinRoot() + L"\\register.log";
                std::wofstream f(path, std::ios::app);
                if (f)
                {
                    SYSTEMTIME st{};
                    GetLocalTime(&st);
                    wchar_t ts[40];
                    swprintf_s(ts, L"%04d-%02d-%02d %02d:%02d:%02d.%03d ",
                               st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
                    f << ts << msg << L"\n";
                }
            }
            catch (...)
            {
            }
        }

        long long NowMs()
        {
            return std::chrono::duration_cast<std::chrono::milliseconds>(
                       std::chrono::steady_clock::now().time_since_epoch())
                .count();
        }

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

        std::wstring FormatVersion(unsigned long long v)
        {
            unsigned a = static_cast<unsigned>((v >> 48) & 0xFFFF);
            unsigned b = static_cast<unsigned>((v >> 32) & 0xFFFF);
            unsigned c = static_cast<unsigned>((v >> 16) & 0xFFFF);
            unsigned d = static_cast<unsigned>(v & 0xFFFF);
            wchar_t buf[64] = {};
            swprintf_s(buf, L"%u.%u.%u.%u", a, b, c, d);
            return buf;
        }

        // Per-version directory holding the service's runnable exe copy.
        std::wstring VersionBinDir()
        {
            std::wstring ver = FormatVersion(GetServiceOwnVersion());
            if (ver.empty())
            {
                ver = L"0";
            }
            return GetServiceBinRoot() + L"\\" + ver;
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
            const long long t0 = NowMs();
            RegLog(L"--register begin sid=" + sid);
            const std::wstring srcExe = OwnExePath();
            if (srcExe.empty())
            {
                return static_cast<int>(GetLastError());
            }

            const std::wstring keyName = ServiceKeyName(sid);
            const std::wstring account = ServiceAccountName(sid);

            // A classic virtual-account service cannot read an exe inside
            // WindowsApps (its ACL grants BUILTIN\Users, not our dedicated
            // account, and can't be modified even elevated -> StartService fails
            // with ERROR_ACCESS_DENIED).  So copy the staged exe into a protected,
            // account-readable %ProgramData% dir and run the service from there
            // (Design §12.8).  Copy happens while the (possibly running) old
            // service is stopped below.
            const std::wstring binDir = VersionBinDir();
            const std::wstring runExe = binDir + L"\\" + kExeName;
            const std::wstring binPath = BuildBinPath(runExe, sid);

            SC_HANDLE scm = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CREATE_SERVICE);
            if (!scm)
            {
                return static_cast<int>(GetLastError());
            }

            int rc = 0;
            bool created = false;
            long long t = NowMs();
            SC_HANDLE svc = OpenServiceW(scm, keyName.c_str(),
                                         SERVICE_CHANGE_CONFIG | SERVICE_STOP |
                                             SERVICE_START | SERVICE_QUERY_STATUS);
            if (svc)
            {
                // Existing (re-install / upgrade): stop first so no exe copy in
                // this version's dir is held open, then re-point below.
                StopAndWait(svc);
                RegLog(L"stop-existing ms=" + std::to_wstring(NowMs() - t));
            }

            // Stage the runnable copy (idempotent; overwrites on re-register).
            t = NowMs();
            EnsureDirectory(GetServiceBinRoot());
            EnsureDirectory(binDir);
            if (!CopyFileW(srcExe.c_str(), runExe.c_str(), FALSE))
            {
                rc = static_cast<int>(GetLastError());
                RegLog(L"copy-exe FAILED rc=" + std::to_wstring(rc));
                if (svc) { CloseServiceHandle(svc); }
                CloseServiceHandle(scm);
                return rc;
            }
            RegLog(L"copy-exe ms=" + std::to_wstring(NowMs() - t));

            t = NowMs();
            if (!svc)
            {
                svc = CreateServiceW(
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
                    rc = static_cast<int>(GetLastError());
                    RegLog(L"CreateService FAILED rc=" + std::to_wstring(rc));
                    CloseServiceHandle(scm);
                    return rc;
                }
                created = true;
                RegLog(L"CreateService ms=" + std::to_wstring(NowMs() - t));
            }
            else
            {
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
                    RegLog(L"ChangeServiceConfig FAILED rc=" + std::to_wstring(rc));
                    CloseServiceHandle(svc);
                    CloseServiceHandle(scm);
                    return rc;
                }
                RegLog(L"ChangeServiceConfig ms=" + std::to_wstring(NowMs() - t));
            }

            // Now the virtual account exists: grant it RX on the bin dir so the
            // service can actually launch (owner=SYSTEM, protected DACL).
            t = NowMs();
            HRESULT hr = ProtectServiceBinDir(binDir, account);
            if (FAILED(hr))
            {
                RegLog(L"ProtectServiceBinDir FAILED hr=" + std::to_wstring(hr));
                CloseServiceHandle(svc);
                CloseServiceHandle(scm);
                return static_cast<int>(hr);
            }
            RegLog(L"ProtectServiceBinDir ms=" + std::to_wstring(NowMs() - t));

            SetFailureActions(svc);

            // Provision the protected store now that the virtual account exists.
            // Idempotent: re-asserts owner=SYSTEM + the protected DACL each run.
            t = NowMs();
            hr = ProvisionStore(GetSettingsRoot(),
                                GetUserFolder(sid),
                                sid,
                                account);
            RegLog(L"ProvisionStore ms=" + std::to_wstring(NowMs() - t) + L" hr=" + std::to_wstring(hr));
            if (FAILED(hr))
            {
                CloseServiceHandle(svc);
                CloseServiceHandle(scm);
                return static_cast<int>(hr);
            }

            if (startAfter)
            {
                t = NowMs();
                if (!StartServiceW(svc, 0, nullptr))
                {
                    DWORD serr = GetLastError();
                    if (serr != ERROR_SERVICE_ALREADY_RUNNING)
                    {
                        rc = static_cast<int>(serr);
                    }
                    RegLog(L"StartService ms=" + std::to_wstring(NowMs() - t) + L" err=" + std::to_wstring(serr));
                }
                else
                {
                    RegLog(L"StartService ms=" + std::to_wstring(NowMs() - t));
                }
            }

            RegLog(L"--register end rc=" + std::to_wstring(rc) +
                   L" total-ms=" + std::to_wstring(NowMs() - t0) +
                   (created ? L" (created)" : L" (repointed)"));
            (void)created;
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
