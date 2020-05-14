#include "pch.h"

#include "version.h"

#include "updating.h"

#include <msi.h>
#include <common/common.h>
#include <common/json.h>
#include <common/settings_helpers.h>
#include <common/winstore.h>
#include <common/notifications.h>

#include <winrt/Windows.Web.Http.h>
#include <winrt/Windows.Web.Http.Headers.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Networking.Connectivity.h>

#include "VersionHelper.h"

namespace
{
    const wchar_t POWER_TOYS_UPGRADE_CODE[] = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";
    const wchar_t DONT_SHOW_AGAIN_RECORD_REGISTRY_PATH[] = L"delete_previous_powertoys_confirm";
    const wchar_t USER_AGENT[] = L"Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
    const wchar_t LATEST_RELEASE_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases/latest";
    const wchar_t MSIX_PACKAGE_NAME[] = L"Microsoft.PowerToys";
    const wchar_t MSIX_PACKAGE_PUBLISHER[] = L"CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

    const wchar_t UPDATE_NOTIFY_TOAST_TAG[] = L"PTUpdateNotifyTag";
    const wchar_t UPDATE_READY_TOAST_TAG[] = L"PTUpdateReadyTag";
    const size_t MAX_DOWNLOAD_ATTEMPTS = 3;
}

namespace localized_strings
{
    const wchar_t OFFER_UNINSTALL_MSI[] = L"We've detected a previous installation of PowerToys. Would you like to remove it?";
    const wchar_t OFFER_UNINSTALL_MSI_TITLE[] = L"PowerToys: uninstall previous version?";
    const wchar_t UNINSTALLATION_SUCCESS[] = L"Previous version of PowerToys was uninstalled successfully.";
    const wchar_t UNINSTALLATION_UNKNOWN_ERROR[] = L"Error: please uninstall the previous version of PowerToys manually.";

    const wchar_t GITHUB_NEW_VERSION_READY_TO_INSTALL[] = L"An update to PowerToys is ready to install.\n";
    const wchar_t GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR[] = L"Error: couldn't download PowerToys installer. Visit our GitHub page to update.\n";
    const wchar_t GITHUB_NEW_VERSION_UPDATE_NOW[] = L"Update now";
    const wchar_t GITHUB_NEW_VERSION_UPDATE_AFTER_RESTART[] = L"At next launch";

    const wchar_t GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT[] = L"An update to PowerToys is available. Visit our GitHub page to update.\n";
    const wchar_t GITHUB_NEW_VERSION_AGREE[] = L"Visit";
    const wchar_t GITHUB_NEW_VERSION_SNOOZE_TITLE[] = L"Click Snooze to be reminded in:";
    const wchar_t GITHUB_NEW_VERSION_UPDATE_SNOOZE_1D[] = L"1 day";
    const wchar_t GITHUB_NEW_VERSION_UPDATE_SNOOZE_5D[] = L"5 days";
}
namespace updating
{
    inline winrt::Windows::Web::Http::HttpClient create_http_client()
    {
        winrt::Windows::Web::Http::HttpClient client;
        auto headers = client.DefaultRequestHeaders();
        headers.UserAgent().TryParseAdd(USER_AGENT);
        return client;
    }

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
            notifications::show_toast(localized_strings::UNINSTALLATION_SUCCESS);
            return true;
        }
        else if (auto system_message = get_last_error_message(uninstall_result); system_message.has_value())
        {
            try
            {
                notifications::show_toast(*system_message);
            }
            catch (...)
            {
                notifications::show_toast(localized_strings::UNINSTALLATION_UNKNOWN_ERROR);
            }
        }
        return false;
    }

    std::future<std::optional<new_version_download_info>> get_new_github_version_info_async()
    {
        try
        {
            auto client = create_http_client();
            auto response = co_await client.GetAsync(winrt::Windows::Foundation::Uri{ LATEST_RELEASE_ENDPOINT });
            (void)response.EnsureSuccessStatusCode();
            const auto body = co_await response.Content().ReadAsStringAsync();
            auto json_body = json::JsonValue::Parse(body).GetObjectW();
            auto new_version = json_body.GetNamedString(L"tag_name");
            winrt::Windows::Foundation::Uri release_page_uri{ json_body.GetNamedString(L"html_url") };

            VersionHelper github_version(winrt::to_string(new_version));
            VersionHelper current_version(VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION);

            if (github_version > current_version)
            {
                const std::wstring_view required_asset_extension = winstore::running_as_packaged() ? L".msix" : L".msi";
                const std::wstring_view required_architecture = get_architecture_string(get_current_architecture());
                constexpr const std::wstring_view required_filename_pattern = updating::installer_filename_pattern;
                for (auto asset_elem : json_body.GetNamedArray(L"assets"))
                {
                    auto asset{ asset_elem.GetObjectW() };
                    std::wstring filename_lower = asset.GetNamedString(L"name", {}).c_str();
                    std::transform(begin(filename_lower), end(filename_lower), begin(filename_lower), ::towlower);

                    const bool extension_matched = filename_lower.ends_with(required_asset_extension);
                    const bool architecture_matched = filename_lower.find(required_architecture) != std::wstring::npos;
                    const bool filename_matched = filename_lower.find(required_filename_pattern) != std::wstring::npos;
                    if (extension_matched && architecture_matched && filename_matched)
                    {
                        winrt::Windows::Foundation::Uri msi_download_url{ asset.GetNamedString(L"browser_download_url") };
                        co_return new_version_download_info{ std::move(release_page_uri), new_version.c_str(), std::move(msi_download_url), std::move(filename_lower) };
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

    std::future<void> try_download_file(const std::filesystem::path& destination, const winrt::Windows::Foundation::Uri& url)
    {
        namespace storage = winrt::Windows::Storage;

        auto client = create_http_client();
        auto response = co_await client.GetAsync(url);
        (void)response.EnsureSuccessStatusCode();
        auto msi_installer_file_stream = co_await storage::Streams::FileRandomAccessStream::OpenAsync(destination.c_str(), storage::FileAccessMode::ReadWrite, storage::StorageOpenOptions::AllowReadersAndWriters, storage::Streams::FileOpenDisposition::CreateAlways);
        co_await response.Content().WriteToStreamAsync(msi_installer_file_stream);
        msi_installer_file_stream.Close();
    }

    std::future<void> try_autoupdate(const bool download_updates_automatically)
    {
        const auto new_version = co_await get_new_github_version_info_async();
        if (!new_version)
        {
            co_return;
        }
        using namespace localized_strings;
        auto current_version_to_next_version = VersionHelper{ VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION }.toWstring();
        current_version_to_next_version += L" -> ";
        current_version_to_next_version += new_version->version_string;

        if (download_updates_automatically && !could_be_costly_connection())
        {
            auto installer_download_dst = get_pending_updates_path();
            std::error_code _;
            std::filesystem::create_directories(installer_download_dst, _);
            installer_download_dst /= new_version->msi_filename;

            bool download_success = false;
            for (size_t i = 0; i < MAX_DOWNLOAD_ATTEMPTS; ++i)
            {
                try
                {
                    co_await try_download_file(installer_download_dst, new_version->msi_download_url);
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
                notifications::toast_params toast_params{ UPDATE_NOTIFY_TOAST_TAG, false };
                std::wstring contents = GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR;
                contents += current_version_to_next_version;

                notifications::show_toast_with_activations(std::move(contents), {}, { notifications::link_button{ GITHUB_NEW_VERSION_AGREE, new_version->release_page_uri.ToString().c_str() } }, std::move(toast_params));

                co_return;
            }

            notifications::toast_params toast_params{ UPDATE_READY_TOAST_TAG, false };
            std::wstring new_version_ready{ GITHUB_NEW_VERSION_READY_TO_INSTALL };
            new_version_ready += current_version_to_next_version;

            notifications::show_toast_with_activations(std::move(new_version_ready),
                                                       {},
                                                       { notifications::link_button{ GITHUB_NEW_VERSION_UPDATE_NOW, L"powertoys://update_now/" },
                                                         notifications::link_button{ GITHUB_NEW_VERSION_UPDATE_AFTER_RESTART, L"powertoys://schedule_update/" },
                                                         notifications::snooze_button{ GITHUB_NEW_VERSION_SNOOZE_TITLE, { { GITHUB_NEW_VERSION_UPDATE_SNOOZE_1D, 24 * 60 }, { GITHUB_NEW_VERSION_UPDATE_SNOOZE_5D, 120 * 60 } } } },
                                                       std::move(toast_params));
        }
        else
        {
            notifications::toast_params toast_params{ UPDATE_NOTIFY_TOAST_TAG, false };
            std::wstring contents = GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT;
            contents += current_version_to_next_version;
            notifications::show_toast_with_activations(std::move(contents), {}, { notifications::link_button{ GITHUB_NEW_VERSION_AGREE, new_version->release_page_uri.ToString().c_str() } }, std::move(toast_params));
        }
    }
}