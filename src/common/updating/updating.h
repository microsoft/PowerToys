#pragma once

#include <optional>
#include <string>
#include <future>
#include <filesystem>
#include <variant>
#include <winrt/Windows.Foundation.h>
//#if __MSVC_VERSION__ >= 1933 // MSVC begin to support std::unexpected in 19.33
#if __has_include(<expected> ) // use the same way with excepted-lite to detect std::unexcepted, as using it as backup
#include <expected>
#define USE_STD_EXPECTED 1
#else
#include <expected.hpp>
#define USE_STD_EXPECTED 0
#endif

#include <common/version/helper.h>

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

    std::future<std::optional<std::filesystem::path>> download_new_version(const new_version_download_info& new_version);
    std::filesystem::path get_pending_updates_path();
#if USE_STD_EXPECTED
    std::future<std::expected<github_version_info, std::wstring>> get_github_version_info_async(const bool prerelease = false);
#else
    std::future<nonstd::expected<github_version_info, std::wstring>> get_github_version_info_async(const bool prerelease = false);
#endif
    void cleanup_updates();

    // non-localized
    constexpr inline std::wstring_view INSTALLER_FILENAME_PATTERN = L"powertoyssetup";
    constexpr inline std::wstring_view INSTALLER_FILENAME_PATTERN_USER = L"powertoysusersetup";
}
