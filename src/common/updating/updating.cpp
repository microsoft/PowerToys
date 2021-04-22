#include "pch.h"

#include <common/version/version.h>
#include <common/version/helper.h>

#include "http_client.h"
#include "notifications.h"
#include "updating.h"

#include <common/notifications/notifications.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/json.h>

namespace // Strings in this namespace should not be localized
{
    const wchar_t LATEST_RELEASE_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases/latest";
    const wchar_t ALL_RELEASES_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases";
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

    std::filesystem::path get_pending_updates_path()
    {
        auto path_str{ PTSettingsHelper::get_root_save_folder_location() };
        path_str += L"\\Updates";
        return { std::move(path_str) };
    }
}
