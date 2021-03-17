#include "pch.h"

#include <common/version/version.h>
#include <common/version/helper.h>

#include "http_client.h"
#include "notifications.h"
#include "updating.h"

#include <common/notifications/notifications.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/json.h>
#include <common/utils/os-detect.h>

namespace // Strings in this namespace should not be localized
{
    const wchar_t LATEST_RELEASE_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases/latest";
    const wchar_t ALL_RELEASES_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases";

    const size_t MAX_DOWNLOAD_ATTEMPTS = 3;
}

namespace updating
{
    std::optional<VersionHelper> extract_version_from_release_object(const json::JsonObject& release_object)
    {
        try
        {
            return VersionHelper{ winrt::to_string(release_object.GetNamedString(L"tag_name")) };
        }
        catch (...)
        {
        }
        return std::nullopt;
    }

    std::pair<Uri, std::wstring> extract_installer_asset_download_info(const json::JsonObject& release_object)
    {
        const std::wstring_view required_architecture = get_architecture_string(get_current_architecture());
        constexpr const std::wstring_view required_filename_pattern = updating::INSTALLER_FILENAME_PATTERN;
        // Desc-sorted by its priority
        const std::array<std::wstring_view, 2> asset_extensions = { L".exe", L".msi" };
        for (const auto asset_extension : asset_extensions)
        {
            for (auto asset_elem : release_object.GetNamedArray(L"assets"))
            {
                auto asset{ asset_elem.GetObjectW() };
                std::wstring filename_lower = asset.GetNamedString(L"name", {}).c_str();
                std::transform(begin(filename_lower), end(filename_lower), begin(filename_lower), ::towlower);

                const bool extension_matched = filename_lower.ends_with(asset_extension);
                const bool architecture_matched = filename_lower.find(required_architecture) != std::wstring::npos;
                const bool filename_matched = filename_lower.find(required_filename_pattern) != std::wstring::npos;
                const bool asset_matched = extension_matched && architecture_matched && filename_matched;
                if (extension_matched && architecture_matched && filename_matched)
                {
                    return std::make_pair(Uri{ asset.GetNamedString(L"browser_download_url") }, std::move(filename_lower));
                }
            }
        }

        throw std::runtime_error("Release object doesn't have the required asset");
    }

    std::future<nonstd::expected<github_version_info, std::wstring>> get_github_version_info_async(const notifications::strings& strings, const bool prerelease)
    {
        // If the current version starts with 0.0.*, it means we're on a local build from a farm and shouldn't check for updates.
        if (VERSION_MAJOR == 0 && VERSION_MINOR == 0)
        {
            co_return nonstd::make_unexpected(strings.GITHUB_NEW_VERSION_USING_LOCAL_BUILD_ERROR);
        }

        try
        {
            http::HttpClient client;
            json::JsonObject release_object;
            const VersionHelper current_version(VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION);
            VersionHelper github_version = current_version;

            // On a <1903 system, block updates to 0.36+ 
            const bool blockNonPatchReleases = current_version.major == 0 && current_version.minor == 35 && !Is19H1OrHigher();

            if (prerelease)
            {
                const auto body = co_await client.request(Uri{ ALL_RELEASES_ENDPOINT });
                for (const auto& json : json::JsonValue::Parse(body).GetArray())
                {
                    auto potential_release_object = json.GetObjectW();
                    const bool is_prerelease = potential_release_object.GetNamedBoolean(L"prerelease", false);
                    auto extracted_version = extract_version_from_release_object(potential_release_object);
                    if (!is_prerelease || !extracted_version || *extracted_version <= github_version)
                    {
                        continue;
                    }
                    // Do not break, since https://developer.github.com/v3/repos/releases/#list-releases
                    // doesn't specify the order in which release object appear
                    github_version = std::move(*extracted_version);
                    release_object = std::move(potential_release_object);
                }
            }
            else
            {
                const auto body = co_await client.request(Uri{ LATEST_RELEASE_ENDPOINT });
                release_object = json::JsonValue::Parse(body).GetObjectW();
                if (auto extracted_version = extract_version_from_release_object(release_object))
                {
                    github_version = *extracted_version;
                }
            }

            if (blockNonPatchReleases && github_version >= VersionHelper{ 0, 36, 0 })
            {
                co_return version_up_to_date{};
            }

            if (github_version <= current_version)
            {
                co_return version_up_to_date{};
            }

            Uri release_page_url{ release_object.GetNamedString(L"html_url") };
            auto installer_download_url = extract_installer_asset_download_info(release_object);
            co_return new_version_download_info{ std::move(release_page_url),
                                                 std::move(github_version),
                                                 std::move(installer_download_url.first),
                                                 std::move(installer_download_url.second) };
        }
        catch (...)
        {
        }
        co_return nonstd::make_unexpected(strings.GITHUB_NEW_VERSION_CHECK_ERROR);
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

    std::future<bool> try_autoupdate(const bool download_updates_automatically, const notifications::strings& strings)
    {
        const auto version_check_result = co_await get_github_version_info_async(strings);
        if (!version_check_result)
        {
            co_return false;
        }
        if (std::holds_alternative<version_up_to_date>(*version_check_result))
        {
            co_return true;
        }
        const auto new_version = std::get<new_version_download_info>(*version_check_result);

        if (download_updates_automatically && !could_be_costly_connection())
        {
            auto installer_download_dst = create_download_path() / new_version.installer_filename;
            bool download_success = false;
            for (size_t i = 0; i < MAX_DOWNLOAD_ATTEMPTS; ++i)
            {
                try
                {
                    http::HttpClient client;
                    co_await client.download(new_version.installer_download_url, installer_download_dst);
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
                updating::notifications::show_install_error(new_version, strings);
                co_return false;
            }

            updating::notifications::show_version_ready(new_version, strings);
        }
        else
        {
            updating::notifications::show_visit_github(new_version, strings);
        }
        co_return true;
    }

    std::future<std::wstring> download_update(const notifications::strings& strings)
    {
        const auto version_check_result = co_await get_github_version_info_async(strings);
        if (!version_check_result || std::holds_alternative<version_up_to_date>(*version_check_result))
        {
            co_return L"";
        }
        const auto new_version = std::get<new_version_download_info>(*version_check_result);
        auto installer_download_dst = create_download_path() / new_version.installer_filename;
        updating::notifications::show_download_start(new_version, strings);

        try
        {
            auto progressUpdateHandle = [&](float progress) {
                updating::notifications::update_download_progress(new_version, progress, strings);
            };

            http::HttpClient client;
            co_await client.download(new_version.installer_download_url, installer_download_dst, progressUpdateHandle);
        }
        catch (...)
        {
            updating::notifications::show_install_error(new_version, strings);
            co_return L"";
        }

        co_return new_version.installer_filename;
    }
}
