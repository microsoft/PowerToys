#include "pch.h"

#include "installer.h"
#include <common/version/version.h>
#include <common/utils/MsiUtils.h>
#include <common/utils/os-detect.h>
#include "utils/winapi_error.h"

#include <common/logger/logger.h>

#include <cstdio>
#include <vector>

#include <wincrypt.h>
#include <wintrust.h>
#include <softpub.h>
#include <winver.h>
#include <MsiQuery.h>

#pragma comment(lib, "wintrust.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "version.lib")
#pragma comment(lib, "Msi.lib")

namespace // Strings in this namespace should not be localized
{
    const wchar_t DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH[] = L"delete_previous_powertoys_confirm";

    const wchar_t TOAST_TITLE[] = L"PowerToys";

    const wchar_t MSIX_PACKAGE_NAME[] = L"Microsoft.PowerToys";
    const wchar_t MSIX_PACKAGE_PUBLISHER[] = L"CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
}

namespace updating
{
    winrt::Windows::Foundation::IAsyncOperation<bool> uninstall_previous_msix_version_async()
    {
        winrt::Windows::Management::Deployment::PackageManager package_manager;

        try
        {
            auto packages = package_manager.FindPackagesForUser({}, MSIX_PACKAGE_NAME, MSIX_PACKAGE_PUBLISHER);
            VersionHelper current_version(VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION, VERSION_BUILD);

            for (auto package : packages)
            {
                VersionHelper msix_version(package.Id().Version().Major, package.Id().Version().Minor, package.Id().Version().Build, package.Id().Version().Revision);

                if (msix_version < current_version)
                {
                    co_await package_manager.RemovePackageAsync(package.Id().FullName());
                    co_return true;
                }
            }
        }
        catch (...)
        {
        }
        co_return false;
    }

    namespace
    {
        // Identity anchors for the official PowerToys installer. These are compile-time constants
        // (never read from the attacker-writable UpdateState.json or from the network), so the
        // identity check also holds for fully offline updates. The .exe bootstrapper version
        // resource reports ProductName "PowerToys (Preview) <arch>" and CompanyName
        // "Microsoft Corporation"; the .msi UpgradeCodes are shared with MsiUtils.h.
        constexpr const wchar_t* MICROSOFT_ORGANIZATION_NAME = L"Microsoft Corporation";
        // NOTE: This must match the WiX bundle ProductName ("PowerToys (Preview) <arch>", see
        // installer/PowerToysSetupVNext/PowerToys.wxs). It is intentionally a prefix so the trailing
        // architecture varies. If the product is ever renamed (e.g. a stable/GA build that drops
        // "(Preview)"), this constant MUST be updated in lockstep, otherwise a legitimate installer
        // would be rejected here in the elevated update path.
        constexpr const wchar_t* POWERTOYS_PRODUCT_NAME_PREFIX = L"PowerToys (Preview)";

        // Reads the signer leaf certificate's Organization (O) from the ALREADY-VERIFIED
        // WinVerifyTrust state and returns true only when it is "Microsoft Corporation". Reading
        // from the verified provider data (instead of re-opening the file by path with
        // CryptQueryObject) binds the publisher check to exactly the signature that was validated
        // against the locked handle, so the file is never read a second time by path.
        bool verified_signer_is_microsoft(CRYPT_PROVIDER_DATA* provData)
        {
            CRYPT_PROVIDER_SGNR* signer = WTHelperGetProvSignerFromChain(provData, 0, FALSE, 0);
            if (!signer || signer->csCertChain == 0)
            {
                return false;
            }

            CRYPT_PROVIDER_CERT* leaf = WTHelperGetProvCertFromChain(signer, 0);
            if (!leaf || !leaf->pCert)
            {
                return false;
            }

            // Match on the certificate's Organization (O) rather than the CN (which can vary,
            // e.g. ".NET"). Compare case-insensitively so a cosmetic casing difference can't cause
            // a false negative. Mutable copy of the OID string: CertGetNameStringW takes a
            // non-const void* type param.
            char organizationOid[] = szOID_ORGANIZATION_NAME;
            const DWORD nameLen = CertGetNameStringW(leaf->pCert, CERT_NAME_ATTR_TYPE, 0, organizationOid, nullptr, 0);
            if (nameLen <= 1)
            {
                return false;
            }

            std::wstring organization(nameLen, L'\0');
            CertGetNameStringW(leaf->pCert, CERT_NAME_ATTR_TYPE, 0, organizationOid, organization.data(), nameLen);
            organization.resize(nameLen - 1); // drop the trailing null terminator

            return _wcsicmp(organization.c_str(), MICROSOFT_ORGANIZATION_NAME) == 0;
        }

        // Cached-only whole-chain revocation, evaluated against the same locked handle. Returns
        // true ONLY when a certificate in the chain is DEFINITIVELY revoked per locally cached
        // revocation data. WTD_CACHE_ONLY_URL_RETRIEVAL keeps this off the network; when revocation
        // info isn't cached the result is "offline/unknown", which is deliberately treated as NOT
        // revoked so a legitimate offline update is never rejected just because a CRL wasn't cached
        // (fail-open on unknown, fail-closed only on a real revocation).
        bool cached_revocation_says_revoked(const std::wstring& installerPath, void* verifiedFileHandle)
        {
            WINTRUST_FILE_INFO fileInfo{};
            fileInfo.cbStruct = sizeof(fileInfo);
            fileInfo.pcwszFilePath = installerPath.c_str();
            fileInfo.hFile = verifiedFileHandle;

            GUID actionGuid = WINTRUST_ACTION_GENERIC_VERIFY_V2;

            WINTRUST_DATA trustData{};
            trustData.cbStruct = sizeof(trustData);
            trustData.dwUIChoice = WTD_UI_NONE;
            trustData.fdwRevocationChecks = WTD_REVOKE_WHOLECHAIN;
            trustData.dwUnionChoice = WTD_CHOICE_FILE;
            trustData.dwStateAction = WTD_STATEACTION_VERIFY;
            trustData.dwProvFlags = WTD_SAFER_FLAG | WTD_CACHE_ONLY_URL_RETRIEVAL;
            trustData.pFile = &fileInfo;

            const LONG status = WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &actionGuid, &trustData);

            trustData.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &actionGuid, &trustData);

            return status == static_cast<LONG>(CERT_E_REVOKED);
        }

        std::wstring read_version_string(const std::vector<BYTE>& versionInfo, WORD language, WORD codePage, const wchar_t* name)
        {
            wchar_t subBlock[128];
            swprintf_s(subBlock, L"\\StringFileInfo\\%04x%04x\\%s", language, codePage, name);

            LPVOID value = nullptr;
            UINT valueLen = 0;
            if (VerQueryValueW(versionInfo.data(), subBlock, &value, &valueLen) && value != nullptr && valueLen > 0)
            {
                std::wstring result{ static_cast<const wchar_t*>(value), valueLen };
                result.resize(wcslen(result.c_str()));
                return result;
            }
            return {};
        }

        // .exe (WiX bootstrapper) identity: version resource CompanyName must be
        // "Microsoft Corporation" and ProductName must start with "PowerToys (Preview)".
        bool exe_version_info_is_powertoys(const std::wstring& installerPath)
        {
            DWORD ignoredHandle = 0;
            const DWORD size = GetFileVersionInfoSizeW(installerPath.c_str(), &ignoredHandle);
            if (size == 0)
            {
                Logger::error(L"Installer '{}' has no version resource (error {:#x})", installerPath, GetLastError());
                return false;
            }

            std::vector<BYTE> versionInfo(size);
            if (!GetFileVersionInfoW(installerPath.c_str(), 0, size, versionInfo.data()))
            {
                Logger::error(L"Couldn't read the version resource of installer '{}' (error {:#x})", installerPath, GetLastError());
                return false;
            }

            struct LangAndCodePage
            {
                WORD language;
                WORD codePage;
            };
            LangAndCodePage* translations = nullptr;
            UINT translationsBytes = 0;
            if (!VerQueryValueW(versionInfo.data(), L"\\VarFileInfo\\Translation", reinterpret_cast<LPVOID*>(&translations), &translationsBytes) ||
                translations == nullptr || translationsBytes < sizeof(LangAndCodePage))
            {
                Logger::error(L"Installer '{}' version resource has no translation table", installerPath);
                return false;
            }

            const size_t translationCount = translationsBytes / sizeof(LangAndCodePage);
            for (size_t i = 0; i < translationCount; ++i)
            {
                const std::wstring company = read_version_string(versionInfo, translations[i].language, translations[i].codePage, L"CompanyName");
                const std::wstring product = read_version_string(versionInfo, translations[i].language, translations[i].codePage, L"ProductName");

                if (_wcsicmp(company.c_str(), MICROSOFT_ORGANIZATION_NAME) == 0 &&
                    product.starts_with(POWERTOYS_PRODUCT_NAME_PREFIX))
                {
                    return true;
                }
            }

            Logger::error(L"Installer '{}' version resource doesn't match the PowerToys bootstrapper identity", installerPath);
            return false;
        }

        // .msi identity: the package UpgradeCode (read read-only from the MSI database, so no
        // custom actions run) must be one of PowerToys' UpgradeCodes (shared with MsiUtils.h).
        bool msi_upgrade_code_is_powertoys(const std::wstring& installerPath)
        {
            PMSIHANDLE database;
            UINT result = MsiOpenDatabaseW(installerPath.c_str(), MSIDBOPEN_READONLY, &database);
            if (result != ERROR_SUCCESS)
            {
                Logger::error(L"Couldn't open installer '{}' as an MSI database (error {})", installerPath, result);
                return false;
            }

            PMSIHANDLE view;
            result = MsiDatabaseOpenViewW(database, L"SELECT `Value` FROM `Property` WHERE `Property` = 'UpgradeCode'", &view);
            if (result != ERROR_SUCCESS)
            {
                Logger::error(L"Couldn't query the UpgradeCode of MSI '{}' (error {})", installerPath, result);
                return false;
            }

            result = MsiViewExecute(view, 0);
            if (result != ERROR_SUCCESS)
            {
                Logger::error(L"Couldn't execute the UpgradeCode query for MSI '{}' (error {})", installerPath, result);
                return false;
            }

            PMSIHANDLE record;
            result = MsiViewFetch(view, &record);
            if (result != ERROR_SUCCESS)
            {
                Logger::error(L"MSI '{}' has no UpgradeCode property (error {})", installerPath, result);
                return false;
            }

            wchar_t upgradeCode[64] = {};
            DWORD upgradeCodeLen = ARRAYSIZE(upgradeCode);
            result = MsiRecordGetStringW(record, 1, upgradeCode, &upgradeCodeLen);
            if (result != ERROR_SUCCESS)
            {
                Logger::error(L"Couldn't read the UpgradeCode value of MSI '{}' (error {})", installerPath, result);
                return false;
            }

            if (_wcsicmp(upgradeCode, POWER_TOYS_UPGRADE_CODE) == 0 ||
                _wcsicmp(upgradeCode, POWER_TOYS_UPGRADE_CODE_USER) == 0)
            {
                return true;
            }

            Logger::error(L"MSI '{}' UpgradeCode {} is not a known PowerToys package", installerPath, upgradeCode);
            return false;
        }

        // Confirms the installer is *the PowerToys installer*, not merely *some* Microsoft-signed
        // binary. Without this, an attacker who can supply a different Microsoft-signed installer or
        // tool could still have it launched elevated by the updater (a confused-deputy elevation).
        bool is_expected_powertoys_installer(const std::wstring& installerPath)
        {
            if (installerPath.ends_with(L".msi"))
            {
                return msi_upgrade_code_is_powertoys(installerPath);
            }
            // Otherwise it is the WiX bootstrapper .exe (matching how Stage 2 launches it).
            return exe_version_info_is_powertoys(installerPath);
        }
    }

    bool verify_installer_trust(const std::wstring& installerPath, void* verifiedFileHandle)
    {
        WINTRUST_FILE_INFO fileInfo{};
        fileInfo.cbStruct = sizeof(fileInfo);
        fileInfo.pcwszFilePath = installerPath.c_str();
        fileInfo.hFile = verifiedFileHandle; // verify the exact bytes we hold open, closing the TOCTOU window
        fileInfo.pgKnownSubject = nullptr;

        GUID actionGuid = WINTRUST_ACTION_GENERIC_VERIFY_V2;

        WINTRUST_DATA trustData{};
        trustData.cbStruct = sizeof(trustData);
        trustData.dwUIChoice = WTD_UI_NONE;
        trustData.fdwRevocationChecks = WTD_REVOKE_NONE;
        trustData.dwUnionChoice = WTD_CHOICE_FILE;
        trustData.dwStateAction = WTD_STATEACTION_VERIFY;
        // WTD_CACHE_ONLY_URL_RETRIEVAL keeps chain building local: without it WinVerifyTrust can
        // reach the network to fetch missing intermediates, which may hang or time out when the
        // update is applied offline. Revocation checks are already disabled above for the same reason.
        trustData.dwProvFlags = WTD_SAFER_FLAG | WTD_CACHE_ONLY_URL_RETRIEVAL;
        trustData.pFile = &fileInfo;

        const LONG status = WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &actionGuid, &trustData);

        // Read the signer from the verified state BEFORE closing it, so the publisher check is
        // bound to exactly the signature just validated against the locked handle.
        bool microsoftSigned = false;
        if (status == ERROR_SUCCESS)
        {
            if (CRYPT_PROVIDER_DATA* provData = WTHelperProvDataFromStateData(trustData.hWVTStateData))
            {
                microsoftSigned = verified_signer_is_microsoft(provData);
            }
        }

        trustData.dwStateAction = WTD_STATEACTION_CLOSE;
        WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &actionGuid, &trustData);

        if (status != ERROR_SUCCESS)
        {
            Logger::error(L"Installer Authenticode trust verification failed for '{}' (status: {:#010x})", installerPath, static_cast<uint32_t>(status));
            return false;
        }

        if (!microsoftSigned)
        {
            // Reaches here both when the signer's organization is not "Microsoft Corporation" and
            // when the signer information couldn't be read from the verified state at all; the
            // wording covers both so it isn't misread as "a specific, known non-Microsoft signer".
            Logger::error(L"Installer '{}' is Authenticode-signed but its signer could not be confirmed as Microsoft Corporation; refusing to run it elevated", installerPath);
            return false;
        }

        // Reject only a chain that is DEFINITIVELY revoked per locally cached revocation data;
        // offline/unknown is treated as not-revoked so offline updates still succeed.
        if (cached_revocation_says_revoked(installerPath, verifiedFileHandle))
        {
            Logger::error(L"Installer '{}' certificate chain is revoked; refusing to run it elevated", installerPath);
            return false;
        }

        // Identity pinning: confirm this is the PowerToys installer, not merely any Microsoft-signed
        // binary, so a different Microsoft-signed installer/tool can't be used for a confused-deputy
        // elevation.
        if (!is_expected_powertoys_installer(installerPath))
        {
            Logger::error(L"Installer '{}' is Microsoft-signed but is not the PowerToys installer; refusing to run it elevated", installerPath);
            return false;
        }

        return true;
    }
}
