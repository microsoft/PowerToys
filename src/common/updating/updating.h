#pragma once

#include <optional>
#include <string>
#include <future>
#include <filesystem>
#include <winrt/Windows.Foundation.h>

#include "notifications.h"
#include "../VersionHelper.h"

namespace updating
{
    std::wstring get_msi_package_path();
    bool uninstall_msi_version(const std::wstring& package_path, const notifications::strings&);
    bool offer_msi_uninstallation(const notifications::strings&);
    std::optional<std::wstring> get_msi_package_installed_path();
    std::optional<VersionHelper> get_installed_powertoys_version();

    std::future<bool> uninstall_previous_msix_version_async();

    struct new_version_download_info
    {
        winrt::Windows::Foundation::Uri release_page_uri;
        std::wstring version_string;
        winrt::Windows::Foundation::Uri installer_download_url;
        std::wstring installer_filename;
    };

    std::future<std::optional<new_version_download_info>> get_new_github_version_info_async();
    std::future<void> try_autoupdate(const bool download_updates_automatically, const notifications::strings&);
    std::filesystem::path get_pending_updates_path();

    std::future<std::wstring> check_new_version_available(const notifications::strings&);
    std::future<std::wstring> download_update(const notifications::strings&);

    // non-localized
    constexpr inline std::wstring_view INSTALLER_FILENAME_PATTERN = L"powertoyssetup";
}