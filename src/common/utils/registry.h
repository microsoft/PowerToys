#pragma once

#include <Windows.h>

#include <functional>
#include <string>
#include <variant>
#include <vector>
#include <optional>
#include <cassert>
#include <sstream>

#include "../logger/logger.h"
#include "../utils/winapi_error.h"
#include "../version/version.h"

namespace registry
{
    namespace detail
    {
        struct on_exit
        {
            std::function<void()> f;

            on_exit(std::function<void()> f) :
                f{ std::move(f) } {}
            ~on_exit() { f(); }
        };

        template<class... Ts>
        struct overloaded : Ts...
        {
            using Ts::operator()...;
        };

        template<class... Ts>
        overloaded(Ts...) -> overloaded<Ts...>;

        inline const wchar_t* getScopeName(HKEY scope)
        {
            if (scope == HKEY_LOCAL_MACHINE)
            {
                return L"HKLM";
            }
            else if (scope == HKEY_CURRENT_USER)
            {
                return L"HKCU";
            }
            else if (scope == HKEY_CLASSES_ROOT)
            {
                return L"HKCR";
            }
            else
            {
                return L"HK??";
            }
        }
    }

    namespace install_scope
    {
        const wchar_t INSTALL_SCOPE_REG_KEY[] = L"Software\\Classes\\powertoys\\";
        const wchar_t UNINSTALL_REG_KEY[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
        
        // Bundle UpgradeCode from PowerToys.wxs (with braces as stored in registry)
        const wchar_t BUNDLE_UPGRADE_CODE[] = L"{6341382D-C0A9-4238-9188-BE9607E3FAB2}";

        enum class InstallScope
        {
            PerMachine = 0,
            PerUser,
        };

        // Helper function to find PowerToys bundle in Windows Uninstall registry by BundleUpgradeCode
        inline bool find_powertoys_bundle_in_uninstall_registry(HKEY rootKey)
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

        inline const InstallScope get_current_install_scope()
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

    template<class>
    inline constexpr bool always_false_v = false;

    struct ValueChange
    {
        using value_t = std::variant<DWORD, std::wstring>;
        static constexpr size_t VALUE_BUFFER_SIZE = 512;

        HKEY scope{};
        std::wstring path;
        std::optional<std::wstring> name; // none == default
        value_t value;
        bool required = true;

        ValueChange(const HKEY scope, std::wstring path, std::optional<std::wstring> name, value_t value, bool required = true) :
            scope{ scope }, path{ std::move(path) }, name{ std::move(name) }, value{ std::move(value) }, required{ required }
        {
        }

        std::wstring toString() const;
        bool isApplied() const;
        bool apply() const;
        bool unApply() const;

        bool requiresElevation() const { return scope == HKEY_LOCAL_MACHINE; }

    private:
        static DWORD valueTypeToWinapiType(const value_t& v);
        static void valueToBuffer(const value_t& value, wchar_t buffer[VALUE_BUFFER_SIZE], DWORD& valueSize, DWORD& type);
        static std::optional<value_t> bufferToValue(const wchar_t buffer[VALUE_BUFFER_SIZE],
                                                    const DWORD valueSize,
                                                    const DWORD type);
    };

    struct ChangeSet
    {
        std::vector<ValueChange> changes;

        bool isApplied() const;
        bool apply() const;
        bool unApply() const;
    };

    const inline std::wstring DOTNET_COMPONENT_CATEGORY_CLSID = L"{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}";
    const inline std::wstring ITHUMBNAIL_PROVIDER_CLSID = L"{E357FCCD-A995-4576-B01F-234630154E96}";
    const inline std::wstring IPREVIEW_HANDLER_CLSID = L"{8895b1c6-b41f-4c1c-a562-0d564250836f}";

    namespace shellex
    {
        enum PreviewHandlerType
        {
            preview,
            thumbnail
        };

        registry::ChangeSet generatePreviewHandler(const PreviewHandlerType handlerType,
                                                          const bool perUser,
                                                          std::wstring handlerClsid,
                                                          std::wstring powertoysVersion,
                                                          std::wstring fullPathToHandler,
                                                          std::wstring className,
                                                          std::wstring displayName,
                                                          std::vector<std::wstring> fileTypes,
                                                          std::wstring perceivedType = L"",
                                                          std::wstring fileKindType = L"");
    }
}
