#include "pch.h"

#include "version.h"

#include "http_client.h"
#include "updating.h"
#include "toast_notifications_helper.h"

#include <msi.h>
#include <common/common.h>
#include <common/json.h>
#include <common/settings_helpers.h>
#include <common/winstore.h>
#include <common/notifications.h>

#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Networking.Connectivity.h>

#include "VersionHelper.h"

namespace
{
    const wchar_t POWER_TOYS_UPGRADE_CODE[] = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";
    const wchar_t DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH[] = L"delete_previous_powertoys_confirm";
    const wchar_t LATEST_RELEASE_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases/latest";
    const wchar_t MSIX_PACKAGE_NAME[] = L"Microsoft.PowerToys";
    const wchar_t MSIX_PACKAGE_PUBLISHER[] = L"CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

    const size_t MAX_DOWNLOAD_ATTEMPTS = 3;
}

namespace localized_strings
{
    const wchar_t OFFER_UNINSTALL_MSI[] = L"We've detected a previous installation of PowerToys. Would you like to remove it?";
    const wchar_t OFFER_UNINSTALL_MSI_TITLE[] = L"PowerToys: uninstall previous version?";
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

    bool offer_msi_uninstallation()
    {
        const auto selection = SHMessageBoxCheckW(nullptr, localized_strings::OFFER_UNINSTALL_MSI, localized_strings::OFFER_UNINSTALL_MSI_TITLE, MB_ICONQUESTION | MB_YESNO, IDNO, DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH);
        return selection == IDYES;
    }

    bool uninstall_msi_version(const std::wstring& package_path)
    {
        const auto uninstall_result = MsiInstallProductW(package_path.c_str(), L"REMOVE=ALL");
        if (ERROR_SUCCESS == uninstall_result)
        {
            notifications::show_uninstallation_success();
            return true;
        }
        else if (auto system_message = get_last_error_message(uninstall_result); system_message.has_value())
        {
            try
            {
                ::notifications::show_toast(*system_message);
            }
            catch (...)
            {
                updating::notifications::show_uninstallation_error();
            }
        }
        return false;
    }

    std::future<std::optional<new_version_download_info>> get_new_github_version_info_async()
    {
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
                constexpr const std::wstring_view required_filename_pattern = updating::installer_filename_pattern;
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

    std::future<void> try_autoupdate(const bool download_updates_automatically)
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
                updating::notifications::show_install_error(new_version.value());
                co_return;
            }

            updating::notifications::show_version_ready(new_version.value());
        }
        else
        {
            updating::notifications::show_visit_github(new_version.value());
        }
    }

    std::future<void> check_new_version_available()
    {
        const auto new_version = co_await get_new_github_version_info_async();
        if (!new_version)
        {
            updating::notifications::show_unavailable();
            co_return;
        }

        updating::notifications::show_available(new_version.value());
    }

    std::future<std::wstring> download_update()
    {
        const auto new_version = co_await get_new_github_version_info_async();
        if (!new_version)
        {
            co_return L"";
        }

        auto installer_download_dst = create_download_path() / new_version->installer_filename;
        updating::notifications::show_download_start(new_version.value());

        try
        {
            auto progressUpdateHandle = [](float progress) {
                updating::notifications::update_download_progress(progress);
            };

            http::HttpClient client;
            co_await client.download(new_version->installer_download_url, installer_download_dst, progressUpdateHandle);
        }
        catch (...)
        {
            updating::notifications::show_install_error(new_version.value());
            co_return L"";
        }

        co_return new_version->installer_filename;
    }
}