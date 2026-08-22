#pragma once

#include <optional>
#include <string>
#include <filesystem>
#include <variant>
#include <winrt/Windows.Foundation.h>
#include <expected>

#include <common/version/helper.h>
#include <wil/coroutine.h>

namespace updating
{
    using winrt::Windows::Foundation::Uri;
    struct version_up_to_date
    {
    };
    struct new_version_download_info
    {
        Uri release_page_uri = nullptr;
        VersionHelper version{ 0, 0, 0 };
        Uri installer_download_url = nullptr;
        std::wstring installer_filename;
    };
    using github_version_info = std::variant<new_version_download_info, version_up_to_date>;
    using github_version_result = std::expected<github_version_info, std::wstring>;

    wil::task<github_version_result> get_github_version_info_async(bool prerelease = false);
    wil::task<std::optional<std::filesystem::path>> download_new_version_async(new_version_download_info new_version);
    std::filesystem::path get_pending_updates_path();
    void cleanup_updates();

    // non-localized
    constexpr inline std::wstring_view INSTALLER_FILENAME_PATTERN = L"powertoyssetup";
    constexpr inline std::wstring_view INSTALLER_FILENAME_PATTERN_USER = L"powertoysusersetup";
}
