#pragma once

#include <optional>
#include <string>
#include <future>
#include <filesystem>

#include <winrt/Windows.Foundation.h>

namespace updating
{
    std::wstring get_msi_package_path();
    bool uninstall_msi_version(const std::wstring& package_path);
    bool offer_msi_uninstallation();

    std::future<bool> uninstall_previous_msix_version_async();

    struct new_version_download_info
    {
        winrt::Windows::Foundation::Uri release_page_uri;
        std::wstring version_string;
        winrt::Windows::Foundation::Uri installer_download_url;
        std::wstring installer_filename;
    };

    std::future<std::optional<new_version_download_info>> get_new_github_version_info_async();
    std::future<void> try_autoupdate(const bool download_updates_automatically);
    std::filesystem::path get_pending_updates_path();

    std::future<void> check_new_version_available();
    std::future<std::wstring> download_update();

    constexpr inline std::wstring_view installer_filename_pattern = L"powertoyssetup";
}