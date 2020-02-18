#pragma once

#include <optional>
#include <string>

std::wstring get_msi_package_path();
bool uninstall_msi_version(const std::wstring& package_path);
bool offer_msi_uninstallation();