#include "pch.h"

#include "version.h"

#include "http_client.h"
#include "notifications.h"
#include "updating.h"

#include <msi.h>
#include <common/common.h>
#include <common/json.h>
#include <common/settings_helpers.h>
#include <common/winstore.h>
#include <common/notifications.h>

#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Networking.Connectivity.h>

#include "VersionHelper.h"
#include <PathCch.h>

namespace // Strings in this namespace should not be localized
{
    const wchar_t POWER_TOYS_UPGRADE_CODE[] = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";
    const wchar_t POWERTOYS_EXE_COMPONENT[] = L"{A2C66D91-3485-4D00-B04D-91844E6B345B}";
    const wchar_t DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH[] = L"delete_previous_powertoys_confirm";
    const wchar_t LATEST_RELEASE_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases/latest";
    const wchar_t MSIX_PACKAGE_NAME[] = L"Microsoft.PowerToys";
    const wchar_t MSIX_PACKAGE_PUBLISHER[] = L"CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

    const size_t MAX_DOWNLOAD_ATTEMPTS = 3;
    const wchar_t TOAST_TITLE[] = L"PowerToys";
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
                updating::notifications::show_uninstallation_error(strings);
            }
        }
        return false;
    }

    std::future<std::optional<new_version_download_info>> get_new_github_version_info_async()
    {
        // If the current version starts with 0.0.*, it means we're on a local build from a farm and shouldn't check for updates.
        if (VERSION_MAJOR == 0 && VERSION_MINOR == 0)
        {
            co_return std::nullopt;
        }
        try
        {
            http::HttpClient client;
            const auto body = co_await client.request(winrt::Windows::Foundation::Uri{ LATEST_RELEASE_ENDPOINT });
            auto json_body = json::JsonValue::Parse(body).GetObjectW();
            auto new_version = json_body.GetNamedString(L"tag_name");
            winrt::Windows::Foundation::Uri release_page_uri{ json_body.GetNamedString(L"html_url") };

            VersionHelper github_version(winrt::to_string(new_version));
            VersionHelper current_version(VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION);

            if (github_version > current_version)
            {
                const std::wstring_view required_architecture = get_architecture_string(get_current_architecture());
                constexpr const std::wstring_view required_filename_pattern = updating::INSTALLER_FILENAME_PATTERN;
                // Desc-sorted by its priority
                const std::array<std::wstring_view, 2> asset_extensions = { L".exe", L".msi" };
                for (const auto asset_extension : asset_extensions)
                {
                    for (auto asset_elem : json_body.GetNamedArray(L"assets"))
                    {
                        auto asset{ asset_elem.GetObjectW() };
                        std::wstring filename_lower = asset.GetNamedString(L"name", {}).c_str();
                        std::transform(begin(filename_lower), end(filename_lower), begin(filename_lower), ::towlower);

                        const bool extension_matched = filename_lower.ends_with(asset_extension);
                        const bool architecture_matched = filename_lower.find(required_architecture) != std::wstring::npos;
                        const bool filename_matched = filename_lower.find(required_filename_pattern) != std::wstring::npos;
                        if (extension_matched && architecture_matched && filename_matched)
                        {
                            winrt::Windows::Foundation::Uri msi_download_url{ asset.GetNamedString(L"browser_download_url") };
                            co_return new_version_download_info{ std::move(release_page_uri), new_version.c_str(), std::move(msi_download_url), std::move(filename_lower) };
                        }
                    }
                }
            }
            else
            {
                co_return std::nullopt;
            }
        }
        catch (...)
        {
            co_return std::nullopt;
        }
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

    bool could_be_costly_connection()
    {
        using namespace winrt::Windows::Networking::Connectivity;
        ConnectionProfile internetConnectionProfile = NetworkInformation::GetInternetConnectionProfile();
        return internetConnectionProfile.IsWwanConnectionProfile();
    }

    std::filesystem::path get_pending_updates_path()
    {
        auto path_str{ PTSettingsHelper::get_root_save_folder_location() };
        path_str += L"\\Updates";
        return { std::move(path_str) };
    }

    std::filesystem::path create_download_path()
    {
        auto installer_download_dst = get_pending_updates_path();
        std::error_code _;
        std::filesystem::create_directories(installer_download_dst, _);
        return installer_download_dst;
    }

    std::future<void> try_autoupdate(const bool download_updates_automatically, const notifications::strings& strings)
    {
        const auto new_version = co_await get_new_github_version_info_async();
        if (!new_version)
        {
            co_return;
        }

        if (download_updates_automatically && !could_be_costly_connection())
        {
            auto installer_download_dst = create_download_path() / new_version->installer_filename;
            bool download_success = false;
            for (size_t i = 0; i < MAX_DOWNLOAD_ATTEMPTS; ++i)
            {
                try
                {
                    http::HttpClient client;
                    co_await client.download(new_version->installer_download_url, installer_download_dst);
                    download_success = true;
                    break;
                }
                catch (...)
                {
                    // reattempt to download or do nothing
                }
            }
            if (!download_success)
            {
                updating::notifications::show_install_error(new_version.value(), strings);
                co_return;
            }

            updating::notifications::show_version_ready(new_version.value(), strings);
        }
        else
        {
            updating::notifications::show_visit_github(new_version.value(), strings);
        }
    }

    std::future<std::wstring> check_new_version_available(const notifications::strings& strings)
    {
        const auto new_version = co_await get_new_github_version_info_async();
        if (!new_version)
        {
            updating::notifications::show_unavailable(strings);
            co_return VersionHelper{ VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION }.toWstring();
        }

        updating::notifications::show_available(new_version.value(), strings);
        co_return new_version->version_string;
    }

    std::future<std::wstring> download_update(const notifications::strings& strings)
    {
        const auto new_version = co_await get_new_github_version_info_async();
        if (!new_version)
        {
            co_return L"";
        }

        auto installer_download_dst = create_download_path() / new_version->installer_filename;
        updating::notifications::show_download_start(new_version.value(), strings);

        try
        {
            auto progressUpdateHandle = [&](float progress) {
                updating::notifications::update_download_progress(new_version.value(), progress, strings);
            };

            http::HttpClient client;
            co_await client.download(new_version->installer_download_url, installer_download_dst, progressUpdateHandle);
        }
        catch (...)
        {
            updating::notifications::show_install_error(new_version.value(), strings);
            co_return L"";
        }

        co_return new_version->installer_filename;
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
}