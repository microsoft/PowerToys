#include "Package.h"

#include <Windows.h>

#include <filesystem>
#include <optional>
#include <fstream>
#include <sstream>
#include <string>

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Management.Deployment.h>

std::optional<winrt::Windows::ApplicationModel::Package> GetPackage(std::wstring packageDisplayName)
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Management::Deployment;

    PackageManager packageManager;

    for (auto const& package : packageManager.FindPackagesForUser({}))
    {
        const auto& packageFullName = std::wstring{ package.Id().FullName() };

        if (packageFullName.contains(packageDisplayName))
        {
            return { package };
        }
    }

    return std::nullopt;
}

std::wstringstream GetPackageInfo(winrt::Windows::ApplicationModel::Package package) {
    std::wstringstream packageInfo;
    packageInfo << L"Display name: " << std::wstring(package.DisplayName()) << std::endl;
    packageInfo << L"Full name: " << std::wstring(package.Id().FullName()) << std::endl;
    packageInfo << L"Version: " << package.Id().Version().Major << L"."
        << package.Id().Version().Minor << L"."
        << package.Id().Version().Build << L"."
        << package.Id().Version().Revision << L"." << std::endl;
    packageInfo << L"Publisher: " << std::wstring(package.Id().Publisher()) << std::endl;
    packageInfo << L"Status: " << (package.Status().VerifyIsOK() ? std::wstring(L"OK") : std::wstring(L"Not OK")) << std::endl;

    return packageInfo;
}

void ReportInstalledContextMenuPackages(const std::filesystem::path& reportDir)
{
    const wchar_t* ImageResizerContextMenuPackageDisplayName = L"ImageResizerContextMenu";
    const wchar_t* PowerRenameContextMenuPackageDisplayName = L"PowerRenameContextMenu";

    auto reportPath = reportDir;
    reportPath.append("context-menu-packages.txt");

    std::wofstream packagesReport(reportPath);

    try
    {
        auto imageResizerPackage = GetPackage(ImageResizerContextMenuPackageDisplayName);
        if (imageResizerPackage)
        {
            packagesReport << GetPackageInfo(*imageResizerPackage).str() << std::endl;
        }

        auto powerRenamePackage = GetPackage(PowerRenameContextMenuPackageDisplayName);
        if (powerRenamePackage)
        {
            packagesReport << GetPackageInfo(*powerRenamePackage).str() << std::endl;
        }
    }
    catch (...)
    {
        printf("Failed to report installed context menu packages");
    }
}
