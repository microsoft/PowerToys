#include "pch.h"

#include "version.h"

#include "http_client.h"
#include "notifications.h"
#include "updating.h"

#include <common/common.h>
#include <common/json.h>
#include <common/settings_helpers.h>
#include <common/winstore.h>
#include <common/notifications.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Networking.Connectivity.h>

#include "VersionHelper.h"

namespace // Strings in this namespace should not be localized
{
    const wchar_t LATEST_RELEASE_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases/latest";

    const size_t MAX_DOWNLOAD_ATTEMPTS = 3;
}

namespace updating
{
    std::future<nonstd::expected<new_version_download_info, std::wstring>> get_new_github_version_info_async(const notifications::strings& strings)
    {
        // If the current version starts with 0.0.*, it means we're on a local build from a farm and shouldn't check for updates.
        if (VERSION_MAJOR == 0 && VERSION_MINOR == 0)
        {
            co_return nonstd::make_unexpected(strings.GITHUB_NEW_VERSION_USING_LOCAL_BUILD_ERROR);
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

            if (github_version <= current_version)
            {
                co_return nonstd::make_unexpected(strings.GITHUB_NEW_VERSION_UP_TO_DATE);
            }

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
        catch (...)
        {
            co_return nonstd::make_unexpected(strings.GITHUB_NEW_VERSION_CHECK_ERROR);
        }
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
        const auto new_version = co_await get_new_github_version_info_async(strings);
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
        auto new_version = co_await get_new_github_version_info_async(strings);
        if (!new_version)
        {
            updating::notifications::show_unavailable(strings, std::move(new_version.error()));
            co_return VersionHelper{ VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION }.toWstring();
        }

        updating::notifications::show_available(new_version.value(), strings);
        co_return new_version->version_string;
    }

    std::future<std::wstring> download_update(const notifications::strings& strings)
    {
        const auto new_version = co_await get_new_github_version_info_async(strings);
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
}
