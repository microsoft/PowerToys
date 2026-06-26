// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include "Generated Files/resource.h"

#include <Windows.h>
#include <shellapi.h>
#include <wincrypt.h>
#include <wintrust.h>
#include <softpub.h>

#include <filesystem>
#include <string_view>
#include <vector>

#include <common/updating/updating.h>
#include <common/updating/updateState.h>
#include <common/updating/installer.h>
#include <common/updating/configBackup.h>
#include <common/updating/updateLifecycle.h>

#include <common/utils/elevation.h>
#include <common/utils/HttpClient.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>

#include <wil/resource.h>

#include <common/SettingsAPI/settings_helpers.h>

#include <common/logger/logger.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Storage.h>
#include <Msi.h>

#include "../runner/tray_icon.h"
#include "../runner/UpdateUtils.h"

using namespace cmdArg;

namespace fs = std::filesystem;

void CleanupStaleTempUpdaters()
{
    // Remove orphaned PowerToys.Update.*.exe files from previous runs
    try
    {
        std::error_code ec;
        const auto tempDir = fs::temp_directory_path();
        for (const auto& entry : fs::directory_iterator(tempDir, ec))
        {
            if (ec)
            {
                break;
            }

            if (!entry.is_regular_file())
            {
                continue;
            }

            const auto filename = entry.path().filename().wstring();
            if (filename.starts_with(L"PowerToys.Update.") && filename.ends_with(L".exe"))
            {
                // Skip our own file (current PID)
                const auto ownFilename = L"PowerToys.Update." + std::to_wstring(GetCurrentProcessId()) + L".exe";
                if (filename == ownFilename)
                {
                    continue;
                }

                fs::remove(entry.path(), ec);
                // Failure to delete is expected if another updater is still running
            }
        }
    }
    catch (...)
    {
        // Best-effort cleanup; don't block the update
    }
}

std::optional<fs::path> CopySelfToTempDir()
{
    CleanupStaleTempUpdaters();

    std::error_code error;
    auto dst_path = fs::temp_directory_path() / (L"PowerToys.Update." + std::to_wstring(GetCurrentProcessId()) + L".exe");
    fs::copy_file(get_module_filename(), dst_path, fs::copy_options::overwrite_existing, error);
    if (error)
    {
        return std::nullopt;
    }

    return dst_path;
}

std::optional<fs::path> ObtainInstaller(bool& isUpToDate)
{
    using namespace updating;

    isUpToDate = false;

    auto state = UpdateState::read();

    // Handle readyToInstall first — the installer is already on disk,
    // so we don't need a GitHub API call (which may fail if offline).
    if (state.state == UpdateState::readyToInstall)
    {
        fs::path installer{ get_pending_updates_path() / state.downloadedInstallerFilename };
        if (fs::is_regular_file(installer))
        {
            return std::move(installer);
        }
        else
        {
            Logger::error(L"Couldn't find a downloaded installer {}", installer.native());
            return std::nullopt;
        }
    }

    if (state.state == UpdateState::upToDate)
    {
        isUpToDate = true;
        return std::nullopt;
    }

    const auto new_version_info = std::move(get_github_version_info_async()).get();

    // Check for error BEFORE dereferencing — the old code crashed here
    // when GitHub API was unreachable (new_version_info held an error string).
    if (!new_version_info)
    {
        Logger::error(L"Couldn't obtain github version info: {}", new_version_info.error());
        return std::nullopt;
    }

    if (std::holds_alternative<version_up_to_date>(*new_version_info))
    {
        isUpToDate = true;
        Logger::error("Invoked with -update_now argument, but no update was available");
        return std::nullopt;
    }

    if (state.state == UpdateState::readyToDownload || state.state == UpdateState::errorDownloading)
    {
        // Cleanup old updates before downloading the latest
        updating::cleanup_updates();

        auto downloaded_installer = std::move(download_new_version_async(std::get<new_version_download_info>(*new_version_info))).get();
        if (!downloaded_installer)
        {
            Logger::error("Couldn't download new installer");
        }

        return downloaded_installer;
    }

    Logger::error("Invoked with -update_now argument, but update state was invalid");
    return std::nullopt;
}

bool InstallNewVersionStage1(fs::path installer)
{
    if (auto copy_in_temp = CopySelfToTempDir())
    {
        // Detect if PT was running
        const auto pt_main_window = FindWindowW(pt_tray_icon_window_class, nullptr);

        if (pt_main_window != nullptr)
        {
            // Get the process that owns the tray window so we can wait for it to exit
            DWORD ptProcessId = 0;
            GetWindowThreadProcessId(pt_main_window, &ptProcessId);

            // Use SendMessageTimeoutW to avoid blocking indefinitely if the
            // tray window thread is hung or unresponsive.
            DWORD_PTR result = 0;
            SendMessageTimeoutW(pt_main_window, WM_CLOSE, 0, 0, SMTO_ABORTIFHUNG, 5000, &result);

            // Wait for PT to actually exit before launching installer.
            // Without this, the installer may find PT files locked.
            if (ptProcessId != 0)
            {
                wil::unique_handle ptProcess{ OpenProcess(SYNCHRONIZE, FALSE, ptProcessId) };
                if (ptProcess)
                {
                    WaitForSingleObject(ptProcess.get(), 10000); // 10 second timeout
                }
            }
        }

        // Pass the install directory so Stage 2 can relaunch PowerToys after install
        const std::wstring installDir = get_module_folderpath();

        std::wstring arguments = updating::BuildStage2Arguments(
            UPDATE_NOW_LAUNCH_STAGE2, installer, fs::path(installDir));
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC };
        sei.lpFile = copy_in_temp->c_str();
        sei.nShow = SW_SHOWNORMAL;

        sei.lpParameters = arguments.c_str();
        return ShellExecuteExW(&sei) == TRUE;
    }
    else
    {
        return false;
    }
}

namespace
{
    // The installer is downloaded into a user-writable directory
    // (%LOCALAPPDATA%\Microsoft\PowerToys\Updates) and is later executed by this
    // updater, possibly elevated. Before running it we must verify it carries a
    // valid Authenticode signature issued to Microsoft. Otherwise a local attacker
    // could plant a malicious binary in that directory and have it executed here.
    constexpr wchar_t EXPECTED_INSTALLER_SIGNER[] = L"Microsoft Corporation";

    bool HasValidAuthenticodeChain(const std::wstring& filePath)
    {
        WINTRUST_FILE_INFO fileInfo{};
        fileInfo.cbStruct = sizeof(fileInfo);
        fileInfo.pcwszFilePath = filePath.c_str();

        WINTRUST_DATA trustData{};
        trustData.cbStruct = sizeof(trustData);
        trustData.dwUIChoice = WTD_UI_NONE;
        trustData.fdwRevocationChecks = WTD_REVOKE_NONE;
        trustData.dwUnionChoice = WTD_CHOICE_FILE;
        trustData.dwStateAction = WTD_STATEACTION_VERIFY;
        trustData.dwProvFlags = WTD_SAFER_FLAG;
        trustData.pFile = &fileInfo;

        GUID policyGuid = WINTRUST_ACTION_GENERIC_VERIFY_V2;
        const LONG status = WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &policyGuid, &trustData);

        // Release the state data regardless of the verification result.
        trustData.dwStateAction = WTD_STATEACTION_CLOSE;
        WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &policyGuid, &trustData);

        return status == ERROR_SUCCESS;
    }

    bool IsSignedByMicrosoft(const std::wstring& filePath)
    {
        HCERTSTORE store = nullptr;
        HCRYPTMSG msg = nullptr;
        DWORD encoding = 0;
        DWORD contentType = 0;
        DWORD formatType = 0;

        if (!CryptQueryObject(CERT_QUERY_OBJECT_FILE,
                              filePath.c_str(),
                              CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                              CERT_QUERY_FORMAT_FLAG_BINARY,
                              0,
                              &encoding,
                              &contentType,
                              &formatType,
                              &store,
                              &msg,
                              nullptr))
        {
            return false;
        }

        auto storeCleanup = wil::scope_exit([&] {
            if (msg)
            {
                CryptMsgClose(msg);
            }
            if (store)
            {
                CertCloseStore(store, 0);
            }
        });

        DWORD signerInfoSize = 0;
        if (!CryptMsgGetParam(msg, CMSG_SIGNER_INFO_PARAM, 0, nullptr, &signerInfoSize) || signerInfoSize == 0)
        {
            return false;
        }

        std::vector<BYTE> signerInfoBuffer(signerInfoSize);
        auto signerInfo = reinterpret_cast<CMSG_SIGNER_INFO*>(signerInfoBuffer.data());
        if (!CryptMsgGetParam(msg, CMSG_SIGNER_INFO_PARAM, 0, signerInfo, &signerInfoSize))
        {
            return false;
        }

        CERT_INFO certInfo{};
        certInfo.Issuer = signerInfo->Issuer;
        certInfo.SerialNumber = signerInfo->SerialNumber;

        PCCERT_CONTEXT certContext = CertFindCertificateInStore(store,
                                                                encoding,
                                                                0,
                                                                CERT_FIND_SUBJECT_CERT,
                                                                &certInfo,
                                                                nullptr);
        if (!certContext)
        {
            return false;
        }

        auto certCleanup = wil::scope_exit([&] { CertFreeCertificateContext(certContext); });

        const DWORD nameLen = CertGetNameStringW(certContext, CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, nullptr, nullptr, 0);
        if (nameLen <= 1)
        {
            return false;
        }

        std::vector<wchar_t> subjectName(nameLen);
        if (CertGetNameStringW(certContext, CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, nullptr, subjectName.data(), nameLen) <= 1)
        {
            return false;
        }

        return std::wstring_view{ subjectName.data() } == EXPECTED_INSTALLER_SIGNER;
    }

    bool IsInstallerTrusted(const std::wstring& installerPath)
    {
        if (!HasValidAuthenticodeChain(installerPath))
        {
            Logger::error(L"Installer failed Authenticode trust verification: {}", installerPath);
            return false;
        }

        if (!IsSignedByMicrosoft(installerPath))
        {
            Logger::error(L"Installer is not signed by Microsoft: {}", installerPath);
            return false;
        }

        return true;
    }
}

bool InstallNewVersionStage2(std::wstring installer_path)
{
    // Never execute an installer from the user-writable Updates directory unless it
    // carries a valid Microsoft Authenticode signature. This is the single execution
    // chokepoint for both freshly-downloaded and previously-downloaded installers.
    if (!IsInstallerTrusted(installer_path))
    {
        Logger::error(L"Refusing to execute installer that failed signature verification: {}", installer_path);
        return false;
    }

    std::transform(begin(installer_path), end(installer_path), begin(installer_path), ::towlower);

    bool success = true;

    if (installer_path.ends_with(L".msi"))
    {
        success = MsiInstallProductW(installer_path.data(), nullptr) == ERROR_SUCCESS;
    }
    else
    {
        // If it's not .msi, then it's a wix bootstrapper
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE };
        sei.lpFile = installer_path.c_str();
        sei.nShow = SW_SHOWNORMAL;
        std::wstring parameters = L"/passive /norestart";
        sei.lpParameters = parameters.c_str();

        success = ShellExecuteExW(&sei) == TRUE;

        // Wait for the install completion
        if (success)
        {
            WaitForSingleObject(sei.hProcess, INFINITE);
            DWORD exitCode = 0;
            GetExitCodeProcess(sei.hProcess, &exitCode);
            success = exitCode == 0;
            CloseHandle(sei.hProcess);
        }
    }

    if (!success)
    {
        return false;
    }

    UpdateState::store([&](UpdateState& state) {
        state = {};
        state.githubUpdateLastCheckedDate.emplace(timeutil::now());
        state.state = UpdateState::upToDate;
    });

    return true;
}

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    int nArgs = 0;
    LPWSTR* args = CommandLineToArgvW(GetCommandLineW(), &nArgs);
    if (!args || nArgs < 2)
    {
        if (args)
        {
            LocalFree(args);
        }
        return 1;
    }

    // D3 fix: ensure args is freed on all exit paths
    auto freeArgs = wil::scope_exit([&] { LocalFree(args); });

    std::wstring_view action{ args[1] };

    std::filesystem::path logFilePath(PTSettingsHelper::get_root_save_folder_location());
    logFilePath.append(LogSettings::updateLogPath);
    Logger::init(LogSettings::updateLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    if (action == UPDATE_NOW_LAUNCH_STAGE1)
    {
        // Backup config files before the update to protect against corruption
        Logger::info("Backing up config files before update");
        auto backupResult = updating::BackupConfigFiles(fs::path(PTSettingsHelper::get_root_save_folder_location()));
        Logger::info("Config backup complete: {} files backed up, {} errors", backupResult.filesBackedUp, backupResult.errors);

        bool isUpToDate = false;
        auto installerPath = ObtainInstaller(isUpToDate);
        bool failed = !installerPath.has_value();
        failed = failed || !InstallNewVersionStage1(std::move(*installerPath));
        if (failed)
        {
            UpdateState::store([&](UpdateState& state) {
                state = {};
                state.githubUpdateLastCheckedDate.emplace(timeutil::now());
                state.state = isUpToDate ? UpdateState::upToDate : UpdateState::errorDownloading;
            });
        }
        return failed;
    }
    else if (action == UPDATE_NOW_LAUNCH_STAGE2)
    {
        if (nArgs < 3)
        {
            Logger::error("Stage 2 invoked without installer path argument");
            return 1;
        }

        using namespace std::string_view_literals;
        const bool failed = !InstallNewVersionStage2(args[2]);
        if (failed)
        {
            UpdateState::store([&](UpdateState& state) {
                state = {};
                state.githubUpdateLastCheckedDate.emplace(timeutil::now());
                state.state = UpdateState::errorDownloading;
            });
        }

        // Always check for corrupted configs after Stage 2, regardless
        // of install success/failure. A failed install may still corrupt configs.
        Logger::info("Checking for corrupted config files after update");
        auto restoreResult = updating::RestoreCorruptedConfigs(fs::path(PTSettingsHelper::get_root_save_folder_location()));
        Logger::info("Config restore check complete: {}/{} files restored, {} errors",
                     restoreResult.filesRestored, restoreResult.filesChecked, restoreResult.errors);

        if (!failed)
        {
            // Relaunch PowerToys from the install directory
            if (updating::CanRelaunchAfterUpdate(nArgs))
            {
                std::wstring ptExePath = updating::BuildPowerToysExePath(args[3]);

                Logger::info(L"Relaunching PowerToys after update: {}", ptExePath);

                SHELLEXECUTEINFOW sei{ sizeof(sei) };
                sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC };
                sei.lpFile = ptExePath.c_str();
                sei.nShow = SW_SHOWNORMAL;
                sei.lpParameters = UPDATE_REPORT_SUCCESS;

                if (!ShellExecuteExW(&sei))
                {
                    Logger::error(L"Failed to relaunch PowerToys after update");
                }
            }
            else
            {
                Logger::warn("Install directory not provided to Stage 2 - cannot relaunch PowerToys");
            }
        }
        return failed;
    }

    return 0;
}
