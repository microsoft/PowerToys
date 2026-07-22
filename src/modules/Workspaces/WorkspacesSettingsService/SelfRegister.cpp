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
#include <filesystem>
#include <userenv.h>

#pragma comment(lib, "userenv.lib")

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

        // Prepares a staging directory to receive the elevated exe copy.  A
        // non-admin could pre-create SettingsSvcBin (or the version subdir) as a
        // junction/symlink to redirect our privileged CopyFile, or as a plain
        // directory with attacker-friendly permissions.  So: refuse to follow a
        // pre-existing reparse point (remove it), then create + apply the
        // admin-only store DACL (SYSTEM/Admins Full, Authenticated Users RX,
        // protected/no-inherit) BEFORE anything is written into it.  The final
        // per-account DACL (adds the virtual account RX, owner=SYSTEM) is applied
        // by ProtectServiceBinDir once the service — hence the account — exists.
        HRESULT EnsureHardenedStagingDir(const std::wstring& dir)
        {
            DWORD attr = GetFileAttributesW(dir.c_str());
            if (attr != INVALID_FILE_ATTRIBUTES && (attr & FILE_ATTRIBUTE_REPARSE_POINT))
            {
                if (!RemoveDirectoryW(dir.c_str()))
                {
                    return HRESULT_FROM_WIN32(GetLastError());
                }
            }
            return EnsureStoreRoot(dir);
        }

        // Upgrade tidy-up: remove every version subfolder under SettingsSvcBin
        // except `keepDir` (the version the service now runs from).  Without this,
        // each upgrade leaves the previous version's exe copy behind and they
        // accumulate.  Best-effort and collected-then-deleted so we don't remove
        // entries mid-iteration; a still-locked old exe is simply left for a
        // later run.
        void PruneOldServiceBinVersions(const std::wstring& keepDir)
        {
            std::error_code ec;
            std::filesystem::path root(GetServiceBinRoot());
            std::filesystem::path keep(keepDir);
            std::vector<std::filesystem::path> toRemove;
            for (std::filesystem::directory_iterator it(root, ec), end; !ec && it != end; it.increment(ec))
            {
                std::error_code isDirEc;
                if (!it->is_directory(isDirEc) || isDirEc)
                {
                    continue;
                }
                std::error_code sameEc;
                bool same = std::filesystem::equivalent(it->path(), keep, sameEc);
                if (sameEc || same)
                {
                    continue; // keep the current version (and be conservative on error)
                }
                toRemove.push_back(it->path());
            }
            for (const auto& p : toRemove)
            {
                std::error_code rmEc;
                std::filesystem::remove_all(p, rmEc);
                RegLog(L"prune-old-bin '" + p.wstring() + L"' ec=" + std::to_wstring(rmEc.value()));
            }
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
            //.  Copy happens while the (possibly running) old
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
            // Harden the staging dirs (reject pre-planted reparse points + apply
            // the admin-only DACL) BEFORE writing the elevated exe into them.
            t = NowMs();
            if (FAILED(EnsureHardenedStagingDir(GetServiceBinRoot())) ||
                FAILED(EnsureHardenedStagingDir(binDir)))
            {
                rc = static_cast<int>(GetLastError());
                if (rc == 0) { rc = static_cast<int>(ERROR_ACCESS_DENIED); }
                RegLog(L"harden-staging-dir FAILED rc=" + std::to_wstring(rc));
                if (svc) { CloseServiceHandle(svc); }
                CloseServiceHandle(scm);
                return rc;
            }
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

            // Now the service runs from binDir; drop any older version copies so
            // upgrades don't accumulate stale exe dirs under SettingsSvcBin.
            PruneOldServiceBinVersions(binDir);

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

    // Best-effort recursive delete; never throws.
    static void RemoveTreeBestEffort(const std::wstring& path)
    {
        if (path.empty())
        {
            return;
        }
        std::error_code ec;
        std::filesystem::remove_all(std::filesystem::path(path), ec);
        RegLog(L"remove-tree '" + path + L"' ec=" + std::to_wstring(ec.value()));
    }

    // Delete the virtual-account profile for PTSettingsSvc_<sid>
    // (C:\Windows\ServiceProfiles\PTSettingsSvc_<sid> + its HKLM ProfileList
    // entry).  DeleteService does NOT remove this, so without it stale profiles
    // accumulate across install/uninstall cycles.  Best-effort.
    static void DeleteServiceAccountProfile(const std::wstring& sid)
    {
        const std::wstring svcName = ServiceKeyName(sid); // PTSettingsSvc_<sid>
        const std::wstring matchSuffix = L"\\" + svcName;

        HKEY hList = nullptr;
        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE,
                          L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList",
                          0, KEY_READ, &hList) == ERROR_SUCCESS)
        {
            std::vector<std::wstring> profileSids;
            wchar_t keyName[256];
            DWORD idx = 0, nameLen = ARRAYSIZE(keyName);
            while (RegEnumKeyExW(hList, idx, keyName, &nameLen, nullptr, nullptr, nullptr, nullptr) == ERROR_SUCCESS)
            {
                HKEY hSub = nullptr;
                if (RegOpenKeyExW(hList, keyName, 0, KEY_READ, &hSub) == ERROR_SUCCESS)
                {
                    wchar_t img[MAX_PATH];
                    DWORD cb = sizeof(img), type = 0;
                    if (RegQueryValueExW(hSub, L"ProfileImagePath", nullptr, &type,
                                         reinterpret_cast<BYTE*>(img), &cb) == ERROR_SUCCESS &&
                        (type == REG_SZ || type == REG_EXPAND_SZ))
                    {
                        std::wstring imgPath(img);
                        if (imgPath.size() >= matchSuffix.size() &&
                            _wcsicmp(imgPath.c_str() + imgPath.size() - matchSuffix.size(),
                                     matchSuffix.c_str()) == 0)
                        {
                            profileSids.emplace_back(keyName);
                        }
                    }
                    RegCloseKey(hSub);
                }
                ++idx;
                nameLen = ARRAYSIZE(keyName);
            }
            RegCloseKey(hList);

            for (const auto& psid : profileSids)
            {
                if (DeleteProfileW(psid.c_str(), nullptr, nullptr))
                {
                    RegLog(L"DeleteProfile ok " + psid);
                }
                else
                {
                    RegLog(L"DeleteProfile gle=" + std::to_wstring(GetLastError()) + L" " + psid);
                }
            }
        }

        // Fallback: remove any leftover profile directory DeleteProfile missed.
        wchar_t winDir[MAX_PATH];
        if (GetWindowsDirectoryW(winDir, ARRAYSIZE(winDir)))
        {
            RemoveTreeBestEffort(std::wstring(winDir) + L"\\ServiceProfiles\\" + svcName);
        }
    }

    // True if any PTSettingsSvc_<other-sid> service still exists.  Used to keep
    // the SHARED runnable-exe dir (SettingsSvcBin) when other users' per-user
    // services still point at it; only the last user removes it.
    static bool AnyOtherPerUserServiceRemains(const std::wstring& excludeSid)
    {
        SC_HANDLE scm = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_ENUMERATE_SERVICE);
        if (!scm)
        {
            return true; // unknown -> conservative (keep the shared bin)
        }
        DWORD need = 0, count = 0, resume = 0;
        EnumServicesStatusExW(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
                              nullptr, 0, &need, &count, &resume, nullptr);
        bool remains = false;
        if (GetLastError() == ERROR_MORE_DATA && need > 0)
        {
            std::vector<BYTE> buf(need);
            if (EnumServicesStatusExW(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
                                      buf.data(), need, &need, &count, &resume, nullptr))
            {
                auto* svcs = reinterpret_cast<ENUM_SERVICE_STATUS_PROCESSW*>(buf.data());
                const std::wstring prefix = L"PTSettingsSvc_";
                const std::wstring self = ServiceKeyName(excludeSid);
                for (DWORD i = 0; i < count; ++i)
                {
                    const wchar_t* n = svcs[i].lpServiceName;
                    if (n && wcsncmp(n, prefix.c_str(), prefix.size()) == 0 && self != n)
                    {
                        remains = true;
                        break;
                    }
                }
            }
        }
        CloseServiceHandle(scm);
        return remains;
    }

    int RunUnregister(const std::wstring& userSidString)
    {
        if (userSidString.empty())
        {
            return static_cast<int>(E_INVALIDARG);
        }

        RegLog(L"--unregister begin sid=" + userSidString);
        const std::wstring keyName = ServiceKeyName(userSidString);

        int rc = 0;
        SC_HANDLE scm = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT);
        if (scm)
        {
            SC_HANDLE svc = OpenServiceW(scm, keyName.c_str(),
                                         SERVICE_STOP | SERVICE_QUERY_STATUS | DELETE);
            if (svc)
            {
                StopAndWait(svc);
                if (!DeleteService(svc))
                {
                    DWORD err = GetLastError();
                    if (err != ERROR_SERVICE_MARKED_FOR_DELETE)
                    {
                        rc = static_cast<int>(err);
                    }
                }
                CloseServiceHandle(svc);
            }
            else if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
            {
                rc = static_cast<int>(GetLastError());
            }
            CloseServiceHandle(scm);
        }
        else
        {
            rc = static_cast<int>(GetLastError());
        }

        // Best-effort per-SID APP-ARTIFACT teardown, always attempted (even if the
        // service was already gone) so a re-run finishes a partial cleanup.
        //
        // The user's protected per-user store (Settings\<sid>) is deliberately
        // PRESERVED — like mainline PowerToys, which keeps user settings under
        // %LocalAppData% on uninstall, the workspaces DATA survives an
        // uninstall/reinstall round-trip.  It stays fully protected while orphaned
        // (SYSTEM-owned, user RX-only, protected DACL — a normal non-admin user
        // still cannot modify it), and on reinstall the same deterministic virtual
        // account re-owns it and ProvisionStore re-asserts the DACL.  Only APP
        // artifacts are removed here: (1) the virtual-account profile, and (2) —
        // when this was the last user — the shared runnable-exe copy.
        DeleteServiceAccountProfile(userSidString);

        const bool removeSharedBin = !AnyOtherPerUserServiceRemains(userSidString);

        // Log the end BEFORE removing the shared bin.  RegLog writes into (and
        // re-creates) SettingsSvcBin, so anything logged AFTER the bin removal
        // would resurrect the very directory we just deleted (leaving an empty
        // SettingsSvcBin\register.log behind).  The bin removal is therefore the
        // last action and is intentionally NOT logged.
        RegLog(L"--unregister end rc=" + std::to_wstring(rc) +
               (removeSharedBin ? L" (removing shared bin)" : L" (shared bin kept)"));

        if (removeSharedBin)
        {
            std::error_code ec;
            std::filesystem::remove_all(std::filesystem::path(GetServiceBinRoot()), ec);
        }

        return rc;
    }
}
