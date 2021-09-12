#include "pch.h"

#include "installer.h"
#include <common/version/version.h>
#include <common/notifications/notifications.h>
#include <common/utils/os-detect.h>
#include "utils/winapi_error.h"

namespace // Strings in this namespace should not be localized
{
    const wchar_t POWER_TOYS_UPGRADE_CODE[] = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";

    const wchar_t DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH[] = L"delete_previous_powertoys_confirm";

    const wchar_t TOAST_TITLE[] = L"PowerToys";

    const wchar_t MSIX_PACKAGE_NAME[] = L"Microsoft.PowerToys";
    const wchar_t MSIX_PACKAGE_PUBLISHER[] = L"CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

    const wchar_t POWERTOYS_EXE_COMPONENT[] = L"{A2C66D91-3485-4D00-B04D-91844E6B345B}";
}

namespace updating
{
    std::wstring get_msi_package_path()
    {
        std::wstring package_path;
        wchar_t GUID_product_string[39];
        if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(POWER_TOYS_UPGRADE_CODE, 0, 0, GUID_product_string); !found)
        {
            return package_path;
        }

        if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(GUID_product_string); !installed)
        {
            return package_path;
        }

        DWORD package_path_size = 0;

        if (const bool has_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &package_path_size); !has_package_path)
        {
            return package_path;
        }

        package_path = std::wstring(++package_path_size, L'\0');
        if (const bool got_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, package_path.data(), &package_path_size); !got_package_path)
        {
            package_path = {};
            return package_path;
        }

        package_path.resize(size(package_path) - 1); // trim additional \0 which we got from MsiGetProductInfoW

        return package_path;
    }

    bool offer_msi_uninstallation(const notifications::strings& strings)
    {
        const auto selection = SHMessageBoxCheckW(nullptr,
                                                  strings.OFFER_UNINSTALL_MSI.c_str(),
                                                  strings.OFFER_UNINSTALL_MSI_TITLE.c_str(),
                                                  MB_ICONQUESTION | MB_YESNO,
                                                  IDNO,
                                                  DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH);
        return selection == IDYES;
    }

    bool uninstall_msi_version(const std::wstring& package_path, const notifications::strings& strings)
    {
        const auto uninstall_result = MsiInstallProductW(package_path.c_str(), L"REMOVE=ALL");
        if (ERROR_SUCCESS == uninstall_result)
        {
            return true;
        }
        else if (auto system_message = get_last_error_message(uninstall_result); system_message.has_value())
        {
            try
            {
                ::notifications::show_toast(*system_message, TOAST_TITLE);
            }
            catch (...)
            {
                MessageBoxW(nullptr, strings.UNINSTALLATION_UNKNOWN_ERROR.c_str(), strings.NOTIFICATION_TITLE.c_str(), MB_OK | MB_ICONERROR);
            }
        }
        return false;
    }

    std::optional<std::wstring> get_msi_package_installed_path()
    {
        constexpr size_t guid_length = 39;
        wchar_t product_ID[guid_length];
        if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(POWER_TOYS_UPGRADE_CODE, 0, 0, product_ID); !found)
        {
            return std::nullopt;
        }

        if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(product_ID); !installed)
        {
            return std::nullopt;
        }

        DWORD buf_size = MAX_PATH;
        wchar_t buf[MAX_PATH];
        if (ERROR_SUCCESS == MsiGetProductInfoW(product_ID, INSTALLPROPERTY_INSTALLLOCATION, buf, &buf_size) && buf_size)
        {
            return buf;
        }

        DWORD package_path_size = 0;

        if (ERROR_SUCCESS != MsiGetProductInfoW(product_ID, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &package_path_size))
        {
            return std::nullopt;
        }
        std::wstring package_path(++package_path_size, L'\0');

        if (ERROR_SUCCESS != MsiGetProductInfoW(product_ID, INSTALLPROPERTY_LOCALPACKAGE, package_path.data(), &package_path_size))
        {
            return std::nullopt;
        }
        package_path.resize(size(package_path) - 1); // trim additional \0 which we got from MsiGetProductInfoW

        wchar_t path[MAX_PATH];
        DWORD path_size = MAX_PATH;
        MsiGetComponentPathW(product_ID, POWERTOYS_EXE_COMPONENT, path, &path_size);
        if (!path_size)
        {
            return std::nullopt;
        }
        PathCchRemoveFileSpec(path, path_size);
        return path;
    }

    std::optional<VersionHelper> get_installed_powertoys_version()
    {
        auto installed_path = get_msi_package_installed_path();
        if (!installed_path)
        {
            return std::nullopt;
        }
        *installed_path += L"\\PowerToys.exe";

        // Get the version information for the file requested
        const DWORD fvSize = GetFileVersionInfoSizeW(installed_path->c_str(), nullptr);
        if (!fvSize)
        {
            return std::nullopt;
        }

        auto pbVersionInfo = std::make_unique<BYTE[]>(fvSize);

        if (!GetFileVersionInfoW(installed_path->c_str(), 0, fvSize, pbVersionInfo.get()))
        {
            return std::nullopt;
        }

        VS_FIXEDFILEINFO* fileInfo = nullptr;
        UINT fileInfoLen = 0;
        if (!VerQueryValueW(pbVersionInfo.get(), L"\\", reinterpret_cast<LPVOID*>(&fileInfo), &fileInfoLen))
        {
            return std::nullopt;
        }
        return VersionHelper{ (fileInfo->dwFileVersionMS >> 16) & 0xffff,
                              (fileInfo->dwFileVersionMS >> 0) & 0xffff,
                              (fileInfo->dwFileVersionLS >> 16) & 0xffff };
    }

    std::future<bool> uninstall_previous_msix_version_async()
    {
        winrt::Windows::Management::Deployment::PackageManager package_manager;

        try
        {
            auto packages = package_manager.FindPackagesForUser({}, MSIX_PACKAGE_NAME, MSIX_PACKAGE_PUBLISHER);
            VersionHelper current_version(VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION);

            for (auto package : packages)
            {
                VersionHelper msix_version(package.Id().Version().Major, package.Id().Version().Minor, package.Id().Version().Revision);

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

    bool is_1809_or_older()
    {
        return !Is19H1OrHigher();
    }
}