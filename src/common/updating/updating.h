#pragma once

#include <optional>
#include <string>
#include <future>
#include <filesystem>
#include <variant>
#include <winrt/Windows.Foundation.h>
#include <expected.hpp>

#include "notifications.h"
#include <common/version/helper.h>

namespace updating
{
    using winrt::Windows::Foundation::Uri;
    struct version_up_to_date {};
    using github_version_info = std::variant<new_version_download_info, version_up_to_date>;

    struct new_version_download_info
    {
        Uri release_page_uri = nullptr;
        VersionHelper version{ 0, 0, 0 };
        Uri installer_download_url = nullptr;
        std::wstring installer_filename;
    };

    // Returns whether the update check has succeeded
    std::future<bool> try_autoupdate(const bool download_updates_automatically, const notifications::strings&);
    std::filesystem::path get_pending_updates_path();
    std::future<std::wstring> download_update(const notifications::strings&);
    std::future<nonstd::expected<github_version_info, std::wstring>> get_github_version_info_async(const notifications::strings& strings, const bool prerelease = true);

    // non-localized
    constexpr inline std::wstring_view INSTALLER_FILENAME_PATTERN = L"powertoyssetup";
}
