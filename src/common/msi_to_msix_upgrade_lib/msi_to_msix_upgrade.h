#pragma once

#include <optional>
#include <string>
#include <future>

#include <winrt/Windows.Foundation.h>

std::wstring get_msi_package_path();
bool uninstall_msi_version(const std::wstring& package_path);
bool offer_msi_uninstallation();

struct new_version_download_info
{
    winrt::Windows::Foundation::Uri release_page_uri;
    std::wstring version_string;
};
std::future<std::optional<new_version_download_info>> check_for_new_github_release_async();
