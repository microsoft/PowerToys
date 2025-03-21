#pragma once

#include <Windows.h>

#include <exception>
#include <filesystem>
#include <regex>
#include <string>
#include <optional>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Management.Deployment.h>

#include "../logger/logger.h"
#include "../version/version.h"

namespace package {

    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::ApplicationModel;
    using namespace winrt::Windows::Management::Deployment;

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

    inline std::optional<Package> GetRegisteredPackage(std::wstring packageDisplayName, bool checkVersion)
    {
        PackageManager packageManager;

        for (const auto& package : packageManager.FindPackagesForUser({}))
        {
            const auto& packageFullName = std::wstring{ package.Id().FullName() };
            const auto& packageVersion = package.Id().Version();

            if (packageFullName.contains(packageDisplayName))
            {
                // If checkVersion is true, verify if the package has the same version as PowerToys.
                if ((!checkVersion) || (packageVersion.Major == VERSION_MAJOR && packageVersion.Minor == VERSION_MINOR && packageVersion.Revision == VERSION_REVISION))
                {
                    return { package };
                }
            }
        }

        return {};
    }

    inline bool IsPackageRegisteredWithPowerToysVersion(std::wstring packageDisplayName)
    {
        return GetRegisteredPackage(packageDisplayName, true).has_value();
    }

    inline bool RegisterSparsePackage(const std::wstring& externalLocation, const std::wstring& sparsePkgPath)
    {
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

    inline bool UnRegisterPackage(const std::wstring& pkgDisplayName)
    {
        try
        {
            PackageManager packageManager;
            const static auto packages = packageManager.FindPackagesForUser({});

            for (auto const& package : packages)
            {
                const auto& packageFullName = std::wstring{ package.Id().FullName() };

                if (packageFullName.contains(pkgDisplayName))
                {
                    auto deploymentOperation{ packageManager.RemovePackageAsync(packageFullName) };
                    deploymentOperation.get();

                    // Check the status of the operation
                    if (deploymentOperation.Status() == AsyncStatus::Error)
                    {
                        auto deploymentResult{ deploymentOperation.GetResults() };
                        auto errorCode = deploymentOperation.ErrorCode();
                        auto errorText = deploymentResult.ErrorText();

                        Logger::error(L"Unregister {} package failed. ErrorCode: {}, ErrorText: {}", packageFullName, std::to_wstring(errorCode), errorText);
                    }
                    else if (deploymentOperation.Status() == AsyncStatus::Canceled)
                    {
                        Logger::error(L"Unregister {} package canceled.", packageFullName);
                    }
                    else if (deploymentOperation.Status() == AsyncStatus::Completed)
                    {
                        Logger::info(L"Unregister {} package completed.", packageFullName);
                    }
                    else
                    {
                        Logger::debug(L"Unregister {} package started.", packageFullName);
                    }

                    break;
                }
            }
        }
        catch (std::exception& e)
        {
            Logger::error("Exception thrown while trying to unregister package: {}", e.what());
            return false;
        }

        return true;
    }

    inline std::vector<std::wstring> FindMsixFile(const std::wstring& directoryPath, bool recursive)
    {
        if (directoryPath.empty())
        {
            return {};
        }

        if (!std::filesystem::exists(directoryPath))
        {
            Logger::error(L"The directory '" + directoryPath + L"' does not exist.");
        }

        const std::regex pattern(R"(^.+\.(appx|msix|msixbundle)$)", std::regex_constants::icase);
        std::vector<std::wstring> matchedFiles;

        try
        {
            if (recursive)
            {
                for (const auto& entry : std::filesystem::recursive_directory_iterator(directoryPath))
                {
                    if (entry.is_regular_file())
                    {
                        const auto& fileName = entry.path().filename().string();
                        if (std::regex_match(fileName, pattern))
                        {
                            matchedFiles.push_back(entry.path());
                        }
                    }
                }
            }
            else
            {
                for (const auto& entry : std::filesystem::directory_iterator(directoryPath))
                {
                    if (entry.is_regular_file())
                    {
                        const auto& fileName = entry.path().filename().string();
                        if (std::regex_match(fileName, pattern))
                        {
                            matchedFiles.push_back(entry.path());
                        }
                    }
                }
            }
        }
        catch (const std::exception& ex)
        {
            Logger::error("An error occurred while searching for MSIX files: " + std::string(ex.what()));
        }

        return matchedFiles;
    }

    inline bool RegisterPackage(std::wstring pkgPath, std::vector<std::wstring> dependencies)
    {
        try
        {
            Uri packageUri{ pkgPath };

            PackageManager packageManager;

            // Declare use of an external location
            DeploymentOptions options = DeploymentOptions::ForceApplicationShutdown;

            Collections::IVector<Uri> uris = winrt::single_threaded_vector<Uri>();
            if (!dependencies.empty())
            {
                for (const auto& dependency : dependencies)
                {
                    try
                    {
                        uris.Append(Uri(dependency));
                    }
                    catch (const winrt::hresult_error& ex)
                    {
                        Logger::error(L"Error creating Uri for dependency: %s", ex.message().c_str());
                    }
                }
            }

            IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> deploymentOperation = packageManager.AddPackageAsync(packageUri, uris, options);
            deploymentOperation.get();

            // Check the status of the operation
            if (deploymentOperation.Status() == AsyncStatus::Error)
            {
                auto deploymentResult{ deploymentOperation.GetResults() };
                auto errorCode = deploymentOperation.ErrorCode();
                auto errorText = deploymentResult.ErrorText();

                Logger::error(L"Register {} package failed. ErrorCode: {}, ErrorText: {}", pkgPath, std::to_wstring(errorCode), errorText);
                return false;
            }
            else if (deploymentOperation.Status() == AsyncStatus::Canceled)
            {
                Logger::error(L"Register {} package canceled.", pkgPath);
                return false;
            }
            else if (deploymentOperation.Status() == AsyncStatus::Completed)
            {
                Logger::info(L"Register {} package completed.", pkgPath);
            }
            else
            {
                Logger::debug(L"Register {} package started.", pkgPath);
            }

        }
        catch (std::exception& e)
        {
            Logger::error("Exception thrown while trying to register package: {}", e.what());

            return false;
        }

        return true;
    }
}