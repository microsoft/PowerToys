#include "pch.h"
#include "registry.h"

#include "../logger/logger.h"

namespace registry
{
    namespace install_scope
    {
        bool find_powertoys_bundle_in_uninstall_registry(HKEY rootKey)
        {
            HKEY uninstallKey{};
            if (RegOpenKeyExW(rootKey, UNINSTALL_REG_KEY, 0, KEY_READ, &uninstallKey) != ERROR_SUCCESS)
            {
                return false;
            }
            detail::on_exit closeUninstallKey{ [uninstallKey] { RegCloseKey(uninstallKey); } };

            DWORD index = 0;
            wchar_t subKeyName[256];

            // Enumerate all subkeys under Uninstall
            while (RegEnumKeyW(uninstallKey, index++, subKeyName, 256) == ERROR_SUCCESS)
            {
                HKEY productKey{};
                if (RegOpenKeyExW(uninstallKey, subKeyName, 0, KEY_READ, &productKey) != ERROR_SUCCESS)
                {
                    continue;
                }
                detail::on_exit closeProductKey{ [productKey] { RegCloseKey(productKey); } };

                // Check BundleUpgradeCode value (specific to WiX Bundle installations)
                wchar_t bundleUpgradeCode[256]{};
                DWORD bundleUpgradeCodeSize = sizeof(bundleUpgradeCode);

                if (RegQueryValueExW(productKey, L"BundleUpgradeCode", nullptr, nullptr,
                                    reinterpret_cast<LPBYTE>(bundleUpgradeCode), &bundleUpgradeCodeSize) == ERROR_SUCCESS)
                {
                    if (_wcsicmp(bundleUpgradeCode, BUNDLE_UPGRADE_CODE) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        const InstallScope get_current_install_scope()
        {
            // 1. Check HKCU Uninstall registry first (user-level bundle)
            // Note: MSI components are always in HKLM regardless of install scope,
            // but the Bundle entry will be in HKCU for per-user installations
            if (find_powertoys_bundle_in_uninstall_registry(HKEY_CURRENT_USER))
            {
                Logger::info(L"Found user-level PowerToys bundle via BundleUpgradeCode in HKCU");
                return InstallScope::PerUser;
            }

            // 2. Check HKLM Uninstall registry (machine-level bundle)
            if (find_powertoys_bundle_in_uninstall_registry(HKEY_LOCAL_MACHINE))
            {
                Logger::info(L"Found machine-level PowerToys bundle via BundleUpgradeCode in HKLM");
                return InstallScope::PerMachine;
            }

            // 3. Fallback to legacy custom registry key detection
            Logger::info(L"PowerToys bundle not found in Uninstall registry, falling back to legacy detection");

            // Open HKLM key
            HKEY perMachineKey{};
            if (RegOpenKeyExW(HKEY_LOCAL_MACHINE,
                              INSTALL_SCOPE_REG_KEY,
                              0,
                              KEY_READ,
                              &perMachineKey) != ERROR_SUCCESS)
            {
                // Open HKCU key
                HKEY perUserKey{};
                if (RegOpenKeyExW(HKEY_CURRENT_USER,
                                  INSTALL_SCOPE_REG_KEY,
                                  0,
                                  KEY_READ,
                                  &perUserKey) != ERROR_SUCCESS)
                {
                    // both keys are missing
                    Logger::warn(L"No PowerToys installation detected, defaulting to PerMachine");
                    return InstallScope::PerMachine;
                }
                else
                {
                    DWORD dataSize{};
                    if (RegGetValueW(
                        perUserKey,
                        nullptr,
                        L"InstallScope",
                        RRF_RT_REG_SZ,
                        nullptr,
                        nullptr,
                        &dataSize) != ERROR_SUCCESS)
                    {
                        // HKCU key is missing
                        RegCloseKey(perUserKey);
                        return InstallScope::PerMachine;
                    }

                    std::wstring data;
                    data.resize(dataSize / sizeof(wchar_t));

                    if (RegGetValueW(
                            perUserKey,
                            nullptr,
                            L"InstallScope",
                            RRF_RT_REG_SZ,
                            nullptr,
                            &data[0],
                            &dataSize) != ERROR_SUCCESS)
                    {
                        // HKCU key is missing
                        RegCloseKey(perUserKey);
                        return InstallScope::PerMachine;
                    }
                    RegCloseKey(perUserKey);

                    if (data.contains(L"perUser"))
                    {
                        return InstallScope::PerUser;
                    }
                }
            }

            return InstallScope::PerMachine;
        }
    }

    namespace shellex
    {
        registry::ChangeSet generatePreviewHandler(const PreviewHandlerType handlerType,
                                                          const bool perUser,
                                                          std::wstring handlerClsid,
                                                          std::wstring powertoysVersion,
                                                          std::wstring fullPathToHandler,
                                                          std::wstring className,
                                                          std::wstring displayName,
                                                          std::vector<std::wstring> fileTypes,
                                                          std::wstring perceivedType,
                                                          std::wstring fileKindType)
        {
            const HKEY scope = perUser ? HKEY_CURRENT_USER : HKEY_LOCAL_MACHINE;

            std::wstring clsidPath = L"Software\\Classes\\CLSID";
            clsidPath += L'\\';
            clsidPath += handlerClsid;

            std::wstring inprocServerPath = clsidPath;
            inprocServerPath += L'\\';
            inprocServerPath += L"InprocServer32";

            std::wstring assemblyKeyValue;
            if (const auto lastDotPos = className.rfind(L'.'); lastDotPos != std::wstring::npos)
            {
                assemblyKeyValue = L"PowerToys." + className.substr(lastDotPos + 1);
            }
            else
            {
                assemblyKeyValue = L"PowerToys." + className;
            }

            assemblyKeyValue += L", Version=";
            assemblyKeyValue += powertoysVersion;
            assemblyKeyValue += L", Culture=neutral";

            std::wstring versionPath = inprocServerPath;
            versionPath += L'\\';
            versionPath += powertoysVersion;

            using vec_t = std::vector<registry::ValueChange>;
            // TODO: verify that we actually need all of those
            vec_t changes = { { scope, clsidPath, L"DisplayName", displayName },
                              { scope, clsidPath, std::nullopt, className },
                              { scope, inprocServerPath, std::nullopt, fullPathToHandler },
                              { scope, inprocServerPath, L"Assembly", assemblyKeyValue },
                              { scope, inprocServerPath, L"Class", className },
                              { scope, inprocServerPath, L"ThreadingModel", L"Apartment" } };

            for (const auto& fileType : fileTypes)
            {
                std::wstring fileTypePath = L"Software\\Classes\\" + fileType;
                std::wstring fileAssociationPath = fileTypePath + L"\\shellex\\";
                fileAssociationPath += handlerType == PreviewHandlerType::preview ? IPREVIEW_HANDLER_CLSID : ITHUMBNAIL_PROVIDER_CLSID;
                changes.push_back({ scope, fileAssociationPath, std::nullopt, handlerClsid });
                if (!fileKindType.empty())
                {
                    // Registering a file type as a kind needs to be done at the HKEY_LOCAL_MACHINE level.
                    // Make it optional as well so that we don't fail registering the handler if we can't write to HKEY_LOCAL_MACHINE.
                    std::wstring kindMapPath = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\KindMap";
                    changes.push_back({ HKEY_LOCAL_MACHINE, kindMapPath, fileType, fileKindType, false});
                }
                if (!perceivedType.empty())
                {
                    changes.push_back({ scope, fileTypePath, L"PerceivedType", perceivedType });
                }
                if (handlerType == PreviewHandlerType::preview && fileType == L".reg")
                {
                    // this regfile registry key has precedence over Software\Classes\.reg for .reg files
                    std::wstring regfilePath = L"Software\\Classes\\regfile\\shellex\\" + IPREVIEW_HANDLER_CLSID + L"\\";
                    changes.push_back({ scope, regfilePath, std::nullopt, handlerClsid });
                }
            }

            if (handlerType == PreviewHandlerType::preview)
            {
                const std::wstring previewHostClsid = L"{6d2b5079-2f0b-48dd-ab7f-97cec514d30b}";
                const std::wstring previewHandlerListPath = LR"(Software\Microsoft\Windows\CurrentVersion\PreviewHandlers)";

                changes.push_back({ scope, clsidPath, L"AppID", previewHostClsid });
                changes.push_back({ scope, previewHandlerListPath, handlerClsid, displayName });
            }

            return registry::ChangeSet{ .changes = std::move(changes) };
        }
    }
}
