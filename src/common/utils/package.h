#pragma once

#include <Windows.h>

#include <exception>
#include <string>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Management.Deployment.h>

#include "../logger/logger.h"
#include "../version/version.h"

namespace package {
    inline BOOL IsWin11OrGreater()
    {
        OSVERSIONINFOEX osvi{};
        DWORDLONG dwlConditionMask = 0;
        byte op = VER_GREATER_EQUAL;

        // Initialize the OSVERSIONINFOEX structure.
        osvi.dwOSVersionInfoSize = sizeof(OSVERSIONINFOEX);
        osvi.dwMajorVersion = HIBYTE(_WIN32_WINNT_WINTHRESHOLD);
        osvi.dwMinorVersion = LOBYTE(_WIN32_WINNT_WINTHRESHOLD);
        // Windows 11 build number
        osvi.dwBuildNumber = 22000;

        // Initialize the condition mask.
        VER_SET_CONDITION(dwlConditionMask, VER_MAJORVERSION, op);
        VER_SET_CONDITION(dwlConditionMask, VER_MINORVERSION, op);
        VER_SET_CONDITION(dwlConditionMask, VER_BUILDNUMBER, op);

        // Perform the test.
        return VerifyVersionInfo(
            &osvi,
            VER_MAJORVERSION | VER_MINORVERSION | VER_BUILDNUMBER,
            dwlConditionMask);
    }

    inline bool IsPackageRegistered(std::wstring packageDisplayName)
    {
        using namespace winrt::Windows::Foundation;
        using namespace winrt::Windows::Management::Deployment;

        PackageManager packageManager;

        for (auto const& package : packageManager.FindPackagesForUser({}))
        {
            const auto& packageFullName = std::wstring{ package.Id().FullName() };
            const auto& packageVersion = package.Id().Version();

            if (packageFullName.contains(packageDisplayName))
            {
                if (packageVersion.Major == VERSION_MAJOR && packageVersion.Minor == VERSION_MINOR && packageVersion.Revision == VERSION_REVISION)
                {
                    return true;
                }
            }
        }

        return false;
    }

    inline bool RegisterSparsePackage(std::wstring externalLocation, std::wstring sparsePkgPath)
    {
        using namespace winrt::Windows::Foundation;
        using namespace winrt::Windows::Management::Deployment;

        try
        {
            Uri externalUri{ externalLocation };
            Uri packageUri{ sparsePkgPath };

            PackageManager packageManager;

            // Declare use of an external location
            AddPackageOptions options;
            options.ExternalLocationUri(externalUri);
            options.ForceUpdateFromAnyVersion(true);

            IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> deploymentOperation = packageManager.AddPackageByUriAsync(packageUri, options);
            deploymentOperation.get();

            // Check the status of the operation
            if (deploymentOperation.Status() == AsyncStatus::Error)
            {
                auto deploymentResult{ deploymentOperation.GetResults() };
                auto errorCode = deploymentOperation.ErrorCode();
                auto errorText = deploymentResult.ErrorText();

                Logger::error(L"Register {} package failed. ErrorCode: {}, ErrorText: {}", sparsePkgPath, std::to_wstring(errorCode), errorText);
                return false;
            }
            else if (deploymentOperation.Status() == AsyncStatus::Canceled)
            {
                Logger::error(L"Register {} package canceled.", sparsePkgPath);
                return false;
            }
            else if (deploymentOperation.Status() == AsyncStatus::Completed)
            {
                Logger::info(L"Register {} package completed.", sparsePkgPath);
            }
            else
            {
                Logger::debug(L"Register {} package started.", sparsePkgPath);
            }

            return true;
        }
        catch (std::exception& e)
        {
            Logger::error("Exception thrown while trying to register package: {}", e.what());

            return false;
        }
    }
}