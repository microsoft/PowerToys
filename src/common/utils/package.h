#pragma once

#include <Windows.h>

#include <algorithm>
#include <appxpackaging.h>
#include <exception>
#include <filesystem>
#include <regex>
#include <string>
#include <optional>
#include <Shlwapi.h>
#include <wrl/client.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Management.Deployment.h>

#include "../logger/logger.h"
#include "../version/version.h"

namespace package
{
    using winrt::Windows::ApplicationModel::Package;
    using winrt::Windows::Foundation::IAsyncOperationWithProgress;
    using winrt::Windows::Foundation::AsyncStatus;
    using winrt::Windows::Foundation::Uri;
    using winrt::Windows::Foundation::Collections::IVector;
    using winrt::Windows::Management::Deployment::AddPackageOptions;
    using winrt::Windows::Management::Deployment::DeploymentOptions;
    using winrt::Windows::Management::Deployment::DeploymentProgress;
    using winrt::Windows::Management::Deployment::DeploymentResult;
    using winrt::Windows::Management::Deployment::PackageManager;
    using Microsoft::WRL::ComPtr;

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

    struct PACKAGE_VERSION
    {
        UINT16 Major;
        UINT16 Minor;
        UINT16 Build;
        UINT16 Revision;
    };

    class ComInitializer
    {
    public:
        explicit ComInitializer(DWORD coInitFlags = COINIT_MULTITHREADED) :
            _initialized(false)
        {
            const HRESULT hr = CoInitializeEx(nullptr, coInitFlags);
            _initialized = SUCCEEDED(hr);
        }

        ~ComInitializer()
        {
            if (_initialized)
            {
                CoUninitialize();
            }
        }

        bool Succeeded() const { return _initialized; }

    private:
        bool _initialized;
    };

    inline bool GetPackageNameAndVersionFromAppx(
        const std::wstring& appxPath,
        std::wstring& outName,
        PACKAGE_VERSION& outVersion)
    {
        try
        {
            ComInitializer comInit;
            if (!comInit.Succeeded())
            {
                Logger::error(L"COM initialization failed.");
                return false;
            }

            ComPtr<IAppxFactory> factory;
            ComPtr<IStream> stream;
            ComPtr<IAppxPackageReader> reader;
            ComPtr<IAppxManifestReader> manifest;
            ComPtr<IAppxManifestPackageId> packageId;

            HRESULT hr = CoCreateInstance(__uuidof(AppxFactory), nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&factory));
            if (FAILED(hr))
                return false;

            hr = SHCreateStreamOnFileEx(appxPath.c_str(), STGM_READ | STGM_SHARE_DENY_WRITE, FILE_ATTRIBUTE_NORMAL, FALSE, nullptr, &stream);
            if (FAILED(hr))
                return false;

            hr = factory->CreatePackageReader(stream.Get(), &reader);
            if (FAILED(hr))
                return false;

            hr = reader->GetManifest(&manifest);
            if (FAILED(hr))
                return false;

            hr = manifest->GetPackageId(&packageId);
            if (FAILED(hr))
                return false;

            LPWSTR name = nullptr;
            hr = packageId->GetName(&name);
            if (FAILED(hr))
                return false;

            UINT64 version = 0;
            hr = packageId->GetVersion(&version);
            if (FAILED(hr))
                return false;

            outName = std::wstring(name);
            CoTaskMemFree(name);

            outVersion.Major = static_cast<UINT16>((version >> 48) & 0xFFFF);
            outVersion.Minor = static_cast<UINT16>((version >> 32) & 0xFFFF);
            outVersion.Build = static_cast<UINT16>((version >> 16) & 0xFFFF);
            outVersion.Revision = static_cast<UINT16>(version & 0xFFFF);

            Logger::info(L"Package name: {}, version: {}.{}.{}.{}, appxPath: {}",
                         outName,
                         outVersion.Major,
                         outVersion.Minor,
                         outVersion.Build,
                         outVersion.Revision,
                         appxPath);

            return true;
        }
        catch (const std::exception& ex)
        {
            Logger::error(L"Standard exception: {}", winrt::to_hstring(ex.what()));
            return false;
        }
        catch (...)
        {
            Logger::error(L"Unknown or non-standard exception occurred.");
            return false;
        }
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
            return {};
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

            // Sort by package version in descending order (newest first)
            std::sort(matchedFiles.begin(), matchedFiles.end(), [](const std::wstring& a, const std::wstring& b) {
                std::wstring nameA, nameB;
                PACKAGE_VERSION versionA{}, versionB{};

                bool gotA = GetPackageNameAndVersionFromAppx(a, nameA, versionA);
                bool gotB = GetPackageNameAndVersionFromAppx(b, nameB, versionB);

                // Files that failed to parse go to the end
                if (!gotA)
                    return false;
                if (!gotB)
                    return true;

                // Compare versions: Major, Minor, Build, Revision (descending)
                if (versionA.Major != versionB.Major)
                    return versionA.Major > versionB.Major;
                if (versionA.Minor != versionB.Minor)
                    return versionA.Minor > versionB.Minor;
                if (versionA.Build != versionB.Build)
                    return versionA.Build > versionB.Build;
                return versionA.Revision > versionB.Revision;
            });
        }
        catch (const std::exception& ex)
        {
            Logger::error("An error occurred while searching for MSIX files: " + std::string(ex.what()));
        }

        return matchedFiles;
    }

    inline bool IsPackageSatisfied(const std::wstring& appxPath)
    {
        std::wstring targetName;
        PACKAGE_VERSION targetVersion{};

        if (!GetPackageNameAndVersionFromAppx(appxPath, targetName, targetVersion))
        {
            Logger::error(L"Failed to get package name and version from appx: " + appxPath);
            return false;
        }

        PackageManager pm;

        for (const auto& package : pm.FindPackagesForUser({}))
        {
            const auto& id = package.Id();
            if (std::wstring(id.Name()) == targetName)
            {
                const auto& version = id.Version();

                if (version.Major > targetVersion.Major ||
                    (version.Major == targetVersion.Major && version.Minor > targetVersion.Minor) ||
                    (version.Major == targetVersion.Major && version.Minor == targetVersion.Minor && version.Build > targetVersion.Build) ||
                    (version.Major == targetVersion.Major && version.Minor == targetVersion.Minor && version.Build == targetVersion.Build && version.Revision >= targetVersion.Revision))
                {
                    Logger::info(
                        L"Package {} is already satisfied with version {}.{}.{}.{}; target version {}.{}.{}.{}; appxPath: {}",
                        id.Name(),
                        version.Major,
                        version.Minor,
                        version.Build,
                        version.Revision,
                        targetVersion.Major,
                        targetVersion.Minor,
                        targetVersion.Build,
                        targetVersion.Revision,
                        appxPath);
                    return true;
                }
            }
        }

        Logger::info(
            L"Package {} is not satisfied. Target version: {}.{}.{}.{}; appxPath: {}",
            targetName,
            targetVersion.Major,
            targetVersion.Minor,
            targetVersion.Build,
            targetVersion.Revision,
            appxPath);
        return false;
    }

    inline bool RegisterPackage(std::wstring pkgPath, std::vector<std::wstring> dependencies)
    {
        try
        {
            Uri packageUri{ pkgPath };

            PackageManager packageManager;

            // Declare use of an external location
            DeploymentOptions options = DeploymentOptions::ForceTargetApplicationShutdown;

            IVector<Uri> uris = winrt::single_threaded_vector<Uri>();
            if (!dependencies.empty())
            {
                for (const auto& dependency : dependencies)
                {
                    try
                    {
                        if (IsPackageSatisfied(dependency))
                        {
                            Logger::info(L"Dependency already satisfied: {}", dependency);
                        }
                        else
                        {
                            uris.Append(Uri(dependency));
                        }
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
