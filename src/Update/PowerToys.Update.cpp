// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include "Generated Files/resource.h"

#include <Windows.h>
#include <shellapi.h>
#include <wincrypt.h>
#include <wintrust.h>
#include <Softpub.h>

#include <wil/resource.h>

#include <filesystem>
#include <string_view>

#include <common/updating/updating.h>
#include <common/updating/updateState.h>
#include <common/updating/installer.h>

#include <common/utils/elevation.h>
#include <common/utils/HttpClient.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>

#include <common/SettingsAPI/settings_helpers.h>

#include <common/logger/logger.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Storage.h>
#include <Msi.h>

#include "../runner/tray_icon.h"
#include "../runner/UpdateUtils.h"

using namespace cmdArg;

namespace fs = std::filesystem;

std::optional<fs::path> CopySelfToTempDir()
{
    std::error_code error;
    auto dst_path = fs::temp_directory_path() / "PowerToys.Update.exe";
    fs::copy_file(get_module_filename(), dst_path, fs::copy_options::overwrite_existing, error);
    if (error)
    {
        return std::nullopt;
    }

    return std::move(dst_path);
}

std::optional<fs::path> ObtainInstaller(bool& isUpToDate)
{
    using namespace updating;

    isUpToDate = false;

    auto state = UpdateState::read();

    const auto new_version_info = std::move(get_github_version_info_async()).get();
    if (std::holds_alternative<version_up_to_date>(*new_version_info))
    {
        isUpToDate = true;
        Logger::error("Invoked with -update_now argument, but no update was available");
        return std::nullopt;
    }

    if (state.state == UpdateState::readyToDownload || state.state == UpdateState::errorDownloading)
    {
        if (!new_version_info)
        {
            Logger::error(L"Couldn't obtain github version info: {}", new_version_info.error());
            return std::nullopt;
        }

        // Cleanup old updates before downloading the latest
        updating::cleanup_updates();

        auto downloaded_installer = std::move(download_new_version_async(std::get<new_version_download_info>(*new_version_info))).get();
        if (!downloaded_installer)
        {
            Logger::error("Couldn't download new installer");
        }

        return downloaded_installer;
    }
    else if (state.state == UpdateState::readyToInstall)
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
    else if (state.state == UpdateState::upToDate)
    {
        isUpToDate = true;
        return std::nullopt;
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
            SendMessageW(pt_main_window, WM_CLOSE, 0, 0);
        }

        std::wstring arguments{ UPDATE_NOW_LAUNCH_STAGE2 };
        arguments += L" \"";
        arguments += installer.c_str();
        arguments += L"\"";
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

// Verifies that the file at installerPath carries a valid Authenticode signature that
// chains to a trusted root and that the signing certificate's organization is
// "Microsoft Corporation". The updater downloads installers and then launches them
// (potentially elevated), so the payload must be confirmed as an official Microsoft-signed
// package before it is executed. The publisher is read from the certificate produced by the
// same WinVerifyTrust verification (rather than re-parsing the file from disk), so the
// organization check always applies to the exact signature that was validated.
static bool IsTrustedMicrosoftSignedInstaller(const std::wstring& installerPath)
{
    WINTRUST_FILE_INFO fileInfo{};
    fileInfo.cbStruct = sizeof(fileInfo);
    fileInfo.pcwszFilePath = installerPath.c_str();

    WINTRUST_DATA trustData{};
    trustData.cbStruct = sizeof(trustData);
    trustData.dwUIChoice = WTD_UI_NONE;
    // Don't perform online revocation checks: they require network access and would
    // turn a legitimate offline update into a verification failure (false negative).
    trustData.fdwRevocationChecks = WTD_REVOKE_NONE;
    // Only use locally cached URLs when building the certificate chain. Without this,
    // WinVerifyTrust can reach out to the network to fetch missing intermediates, which
    // could hang or time out during an offline update.
    trustData.dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL;
    trustData.dwUnionChoice = WTD_CHOICE_FILE;
    trustData.dwStateAction = WTD_STATEACTION_VERIFY;
    trustData.pFile = &fileInfo;

    GUID actionId = WINTRUST_ACTION_GENERIC_VERIFY_V2;
    const LONG trustStatus = WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &actionId, &trustData);

    bool isMicrosoftSigned = false;

    if (trustStatus == ERROR_SUCCESS)
    {
        // Read the signer certificate from the verified state instead of re-opening the file,
        // so the publisher check is bound to exactly the signature WinVerifyTrust validated.
        if (CRYPT_PROVIDER_DATA* provData = WTHelperProvDataFromStateData(trustData.hWVTStateData))
        {
            if (CRYPT_PROVIDER_SGNR* signer = WTHelperGetProvSignerFromChain(provData, 0, FALSE, 0))
            {
                if (CRYPT_PROVIDER_CERT* signerCert = WTHelperGetProvCertFromChain(signer, 0))
                {
                    // CertGetNameStringW takes a non-const void* for the OID parameter.
                    char organizationOid[] = szOID_ORGANIZATION_NAME;
                    const DWORD nameLen = CertGetNameStringW(signerCert->pCert, CERT_NAME_ATTR_TYPE, 0, organizationOid, nullptr, 0);
                    if (nameLen > 1)
                    {
                        std::wstring organization(nameLen, L'\0');
                        CertGetNameStringW(signerCert->pCert, CERT_NAME_ATTR_TYPE, 0, organizationOid, organization.data(), nameLen);
                        organization.resize(wcslen(organization.c_str()));
                        isMicrosoftSigned = _wcsicmp(organization.c_str(), L"Microsoft Corporation") == 0;
                        if (!isMicrosoftSigned)
                        {
                            Logger::error(L"Installer signed by an unexpected publisher: {}", organization);
                        }
                    }
                }
            }
        }

        if (!isMicrosoftSigned)
        {
            Logger::error(L"Couldn't confirm the installer is signed by Microsoft Corporation: {}", installerPath);
        }
    }
    else
    {
        Logger::error(L"Installer failed Authenticode verification: {} (status {:#x})", installerPath, static_cast<uint32_t>(trustStatus));
    }

    // Always release the verification state allocated above.
    trustData.dwStateAction = WTD_STATEACTION_CLOSE;
    WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &actionId, &trustData);

    return isMicrosoftSigned;
}

bool InstallNewVersionStage2(std::wstring installer_path)
{
    // Hold a read-only, deny-write handle on the installer across verification AND execution,
    // so the validated file cannot be swapped between the signature check and running it (TOCTOU).
    // The handle allows shared reads (so msiexec / the bootstrapper can still open it) but blocks
    // writes and deletes by other processes for as long as this function is on the stack.
    wil::unique_hfile installerLock{ CreateFileW(installer_path.c_str(),
                                                 GENERIC_READ,
                                                 FILE_SHARE_READ,
                                                 nullptr,
                                                 OPEN_EXISTING,
                                                 FILE_ATTRIBUTE_NORMAL,
                                                 nullptr) };
    if (!installerLock)
    {
        Logger::error(L"Couldn't open the installer for verification: {} (error {:#x})", installer_path, GetLastError());
        return false;
    }

    // Never execute an installer that isn't an official Microsoft-signed package.
    if (!IsTrustedMicrosoftSignedInstaller(installer_path))
    {
        Logger::error(L"Refusing to install unverified installer: {}", installer_path);
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
        return 1;
    }

    std::wstring_view action{ args[1] };

    std::filesystem::path logFilePath(PTSettingsHelper::get_root_save_folder_location());
    logFilePath.append(LogSettings::updateLogPath);
    Logger::init(LogSettings::updateLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    if (action == UPDATE_NOW_LAUNCH_STAGE1)
    {
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
        return failed;
    }

    return 0;
}
