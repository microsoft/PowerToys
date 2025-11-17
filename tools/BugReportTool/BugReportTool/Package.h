#pragma once

#include <filesystem>
#include <optional>
#include <sstream>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Management.Deployment.h>

std::optional<winrt::Windows::ApplicationModel::Package> GetPackage(std::wstring packageDisplayName);
std::wstringstream GetPackageInfo(winrt::Windows::ApplicationModel::Package package);
void ReportInstalledContextMenuPackages(const std::filesystem::path& reportDir);
