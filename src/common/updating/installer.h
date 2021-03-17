#pragma once

#include <string>
#include <optional>
#include <future>

#include "notifications.h"
#include <common/version/helper.h>

namespace updating
{
    std::wstring get_msi_package_path();
    bool uninstall_msi_version(const std::wstring& package_path, const notifications::strings&);
    bool offer_msi_uninstallation(const notifications::strings&);
    std::optional<std::wstring> get_msi_package_installed_path();

    std::optional<VersionHelper> get_installed_powertoys_version();
    std::future<bool> uninstall_previous_msix_version_async();

    bool is_old_windows_version();
}