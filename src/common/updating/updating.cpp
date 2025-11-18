#include "pch.h"

#include <common/utils/HttpClient.h>
#include <common/utils/string_utils.h>
#include <common/version/version.h>
#include <common/version/helper.h>

#include "updating.h"

#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/json.h>
#include <common/utils/registry.h>

using namespace registry::install_scope;

namespace // Strings in this namespace should not be localized
{
    const wchar_t LATEST_RELEASE_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases/latest";
    const wchar_t ALL_RELEASES_ENDPOINT[] = L"https://api.github.com/repos/microsoft/PowerToys/releases";

    const wchar_t LOCAL_BUILD_ERROR[] = L"Local build cannot be updated";
    const wchar_t NETWORK_ERROR[] = L"Network error";

    const size_t MAX_DOWNLOAD_ATTEMPTS = 3;
}

namespace updating
{
    Uri extract_release_page_url(const json::JsonObject& release_object)
    {
        try
        {
            return Uri{ release_object.GetNamedString(L"html_url") };
        }
        catch (...)
        {
        }
        return nullptr;
    }

    std::optional<VersionHelper> extract_version_from_release_object(const json::JsonObject& release_object)
    {
        return VersionHelper::fromString(release_object.GetNamedString(L"tag_name"));
    }

    std::pair<Uri, std::wstring> extract_installer_asset_download_info(const json::JsonObject& release_object)
    {
        const std::wstring_view required_architecture = get_architecture_string(get_current_architecture());
        std::wstring_view required_filename_pattern = updating::INSTALLER_FILENAME_PATTERN;
        // Desc-sorted by its priority
        const std::array<std::wstring_view, 2> asset_extensions = { L".exe", L".msi" };

        const InstallScope current_install_scope = get_current_install_scope();
        if (current_install_scope == InstallScope::PerUser)
        {
            required_filename_pattern = updating::INSTALLER_FILENAME_PATTERN_USER;
        }

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
                if (asset_matched)
                {
                    return std::make_pair(Uri{ asset.GetNamedString(L"browser_download_url") }, std::move(filename_lower));
                }
            }
        }

        throw std::runtime_error("Release object doesn't have the required asset");
    }

// disabling warning 4702 - unreachable code
// prevent the warning that may show up depend on the value of the constants (#defines)
#pragma warning(push)
#pragma warning(disable : 4702)
#if USE_STD_EXPECTED
    std::future<std::expected<github_version_info, std::wstring>> get_github_version_info_async(const bool prerelease)
#else
    std::future<nonstd::expected<github_version_info, std::wstring>> get_github_version_info_async(const bool prerelease)
#endif
    {
        // If the current version starts with 0.0.*, it means we're on a local build from a farm and shouldn't check for updates.
        if constexpr (VERSION_MAJOR == 0 && VERSION_MINOR == 0)
        {
#if USE_STD_EXPECTED
            co_return std::unexpected(LOCAL_BUILD_ERROR);
#else
            co_return nonstd::make_unexpected(LOCAL_BUILD_ERROR);
#endif
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

            auto [installer_download_url, installer_filename] = extract_installer_asset_download_info(release_object);
            co_return new_version_download_info{ extract_release_page_url(release_object),
                                                 std::move(github_version),
                                                 std::move(installer_download_url),
                                                 std::move(installer_filename) };
        }
        catch (...)
        {
        }
#if USE_STD_EXPECTED
        co_return std::unexpected(NETWORK_ERROR);
#else
        co_return nonstd::make_unexpected(NETWORK_ERROR);
#endif
    }
#pragma warning(pop)

    std::filesystem::path get_pending_updates_path()
    {
        auto path_str{ PTSettingsHelper::get_root_save_folder_location() };
        path_str += L"\\Updates";
        return { std::move(path_str) };
    }

    std::optional<std::filesystem::path> create_download_path()
    {
        auto installer_download_path = get_pending_updates_path();
        std::error_code ec;
        std::filesystem::create_directories(installer_download_path, ec);
        return !ec ? std::optional{ installer_download_path } : std::nullopt;
    }

    std::future<std::optional<std::filesystem::path>> download_new_version(const new_version_download_info& new_version)
    {
        auto installer_download_path = create_download_path();
        if (!installer_download_path)
        {
            co_return std::nullopt;
        }

        *installer_download_path /= new_version.installer_filename;

        bool download_success = false;
        for (size_t i = 0; i < MAX_DOWNLOAD_ATTEMPTS; ++i)
        {
            try
            {
                http::HttpClient client;
                co_await client.download(new_version.installer_download_url, *installer_download_path);
                download_success = true;
                break;
            }
            catch (...)
            {
                // reattempt to download or do nothing
            }
        }
        co_return download_success ? installer_download_path : std::nullopt;
    }

    void cleanup_updates()
    {
        auto update_dir = updating::get_pending_updates_path();
        if (std::filesystem::exists(update_dir))
        {
            // Msi and exe files
            for (const auto& entry : std::filesystem::directory_iterator(update_dir))
            {
                auto entryPath = entry.path().wstring();
                std::transform(entryPath.begin(), entryPath.end(), entryPath.begin(), ::towlower);

                if (entryPath.ends_with(L".msi") || entryPath.ends_with(L".exe"))
                {
                    std::error_code err;
                    std::filesystem::remove(entry, err);
                    if (err.value())
                    {
                        Logger::warn("Failed to delete installer file {}. {}", entry.path().string(), err.message());
                    }
                }
            }
        }

        // Log files
        auto rootPath{ PTSettingsHelper::get_root_save_folder_location() };
        auto currentVersion = left_trim<wchar_t>(get_product_version(), L"v");
        if (std::filesystem::exists(rootPath))
        {
            for (const auto& entry : std::filesystem::directory_iterator(rootPath))
            {
                auto entryPath = entry.path().wstring();
                std::transform(entryPath.begin(), entryPath.end(), entryPath.begin(), ::towlower);
                if (entry.is_regular_file() && entryPath.ends_with(L".log") && entryPath.find(currentVersion) == std::string::npos)
                {
                    std::error_code err;
                    std::filesystem::remove(entry, err);
                    if (err.value())
                    {
                        Logger::warn("Failed to delete log file {}. {}", entry.path().string(), err.message());
                    }
                }
            }
        }
    }
}
