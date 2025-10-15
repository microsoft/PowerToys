<<<<<<< HEAD
#include "pch.h"
#include "package.h"
#include <common/utils/winapi_error.h>

=======
﻿#include "pch.h"
#include "package.h"

#include <common/logger/logger.h>
#include <common/version/version.h>
#include <common/utils/winapi_error.h>

#include <Shlwapi.h>
#include <wrl/client.h>

using Microsoft::WRL::ComPtr;

>>>>>>> 2978ce163a (dev)
namespace package
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::ApplicationModel;
    using namespace winrt::Windows::Management::Deployment;
<<<<<<< HEAD
    using Microsoft::WRL::ComPtr;

    bool GetPackageNameAndVersionFromAppx(
        const std::wstring& appxPath,
        std::wstring& outName,
        PACKAGE_VERSION& outVersion)
=======

    // Windows 11 is Windows 10 with build >= 22000
    BOOL IsWin11OrGreater()
    {
        OSVERSIONINFOEX osvi{};
        osvi.dwOSVersionInfoSize = sizeof(OSVERSIONINFOEX);
        osvi.dwMajorVersion = HIBYTE(_WIN32_WINNT_WINTHRESHOLD); // 10
        osvi.dwMinorVersion = LOBYTE(_WIN32_WINNT_WINTHRESHOLD); // 0
        osvi.dwBuildNumber = 22000; // Windows 11 RTM build

        DWORDLONG mask = 0;
        BYTE op = VER_GREATER_EQUAL;
        VER_SET_CONDITION(mask, VER_MAJORVERSION, op);
        VER_SET_CONDITION(mask, VER_MINORVERSION, op);
        VER_SET_CONDITION(mask, VER_BUILDNUMBER, op);

        return VerifyVersionInfo(&osvi,
                                 VER_MAJORVERSION | VER_MINORVERSION | VER_BUILDNUMBER,
                                 mask);
    }

    ComInitializer::ComInitializer(DWORD coInitFlags) : _initialized(false)
    {
        const HRESULT hr = CoInitializeEx(nullptr, coInitFlags);
        _initialized = SUCCEEDED(hr);
    }

    ComInitializer::~ComInitializer()
    {
        if (_initialized)
        {
            CoUninitialize();
        }
    }

    bool ComInitializer::Succeeded() const
    {
        return _initialized;
    }

    static inline int compare_versions(const PackageVersion& a, const PACKAGE_VERSION& b)
    {
        if (a.Major != b.Major) return (a.Major < b.Major) ? -1 : 1;
        if (a.Minor != b.Minor) return (a.Minor < b.Minor) ? -1 : 1;
        if (a.Build != b.Build) return (a.Build < b.Build) ? -1 : 1;
        if (a.Revision != b.Revision) return (a.Revision < b.Revision) ? -1 : 1;
        return 0;
    }

    bool GetPackageNameAndVersionFromAppx(const std::wstring& appxPath,
                                          std::wstring& outName,
                                          PACKAGE_VERSION& outVersion)
>>>>>>> 2978ce163a (dev)
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
<<<<<<< HEAD
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
=======
            {
                Logger::error(L"CoCreateInstance(AppxFactory) failed. {}", get_last_error_or_default(hr));
                return false;
            }

            hr = SHCreateStreamOnFileEx(appxPath.c_str(), STGM_READ | STGM_SHARE_DENY_WRITE, FILE_ATTRIBUTE_NORMAL, FALSE, nullptr, &stream);
            if (FAILED(hr))
            {
                Logger::error(L"SHCreateStreamOnFileEx failed. {}", get_last_error_or_default(hr));
                return false;
            }

            hr = factory->CreatePackageReader(stream.Get(), &reader);
            if (FAILED(hr))
            {
                Logger::error(L"CreatePackageReader failed. {}", get_last_error_or_default(hr));
                return false;
            }

            hr = reader->GetManifest(&manifest);
            if (FAILED(hr))
            {
                Logger::error(L"GetManifest failed. {}", get_last_error_or_default(hr));
                return false;
            }

            hr = manifest->GetPackageId(&packageId);
            if (FAILED(hr))
            {
                Logger::error(L"GetPackageId failed. {}", get_last_error_or_default(hr));
                return false;
            }
>>>>>>> 2978ce163a (dev)

            LPWSTR name = nullptr;
            hr = packageId->GetName(&name);
            if (FAILED(hr))
<<<<<<< HEAD
                return false;

            UINT64 version = 0;
            hr = packageId->GetVersion(&version);
            if (FAILED(hr))
                return false;
=======
            {
                Logger::error(L"GetName failed. {}", get_last_error_or_default(hr));
                return false;
            }

            UINT64 ver64 = 0;
            hr = packageId->GetVersion(&ver64);
            if (FAILED(hr))
            {
                CoTaskMemFree(name);
                Logger::error(L"GetVersion failed. {}", get_last_error_or_default(hr));
                return false;
            }
>>>>>>> 2978ce163a (dev)

            outName = std::wstring(name);
            CoTaskMemFree(name);

<<<<<<< HEAD
            outVersion.Major = static_cast<UINT16>((version >> 48) & 0xFFFF);
            outVersion.Minor = static_cast<UINT16>((version >> 32) & 0xFFFF);
            outVersion.Build = static_cast<UINT16>((version >> 16) & 0xFFFF);
            outVersion.Revision = static_cast<UINT16>(version & 0xFFFF);
=======
            outVersion.Major = static_cast<UINT16>((ver64 >> 48) & 0xFFFF);
            outVersion.Minor = static_cast<UINT16>((ver64 >> 32) & 0xFFFF);
            outVersion.Build = static_cast<UINT16>((ver64 >> 16) & 0xFFFF);
            outVersion.Revision = static_cast<UINT16>(ver64 & 0xFFFF);
>>>>>>> 2978ce163a (dev)

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

    std::optional<Package> GetRegisteredPackage(std::wstring packageDisplayName, bool checkVersion)
    {
        PackageManager packageManager;
<<<<<<< HEAD

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

=======
        for (const auto& pkg : packageManager.FindPackagesForUser({}))
        {
            const auto& fullName = std::wstring{ pkg.Id().FullName() };
            if (fullName.find(packageDisplayName) != std::wstring::npos)
            {
                if (!checkVersion)
                {
                    return pkg;
                }
                const auto& ver = pkg.Id().Version();
                if (ver.Major == VERSION_MAJOR && ver.Minor == VERSION_MINOR && ver.Revision == VERSION_REVISION)
                {
                    return pkg;
                }
            }
        }
        return {};
    }

    bool IsPackageRegisteredWithPowerToysVersion(std::wstring packageDisplayName)
    {
        return GetRegisteredPackage(packageDisplayName, true).has_value();
    }

>>>>>>> 2978ce163a (dev)
    bool RegisterSparsePackage(const std::wstring& externalLocation, const std::wstring& sparsePkgPath)
    {
        try
        {
            Uri externalUri{ externalLocation };
            Uri packageUri{ sparsePkgPath };

            PackageManager packageManager;

<<<<<<< HEAD
            // Declare use of an external location
=======
>>>>>>> 2978ce163a (dev)
            AddPackageOptions options;
            options.ExternalLocationUri(externalUri);
            options.ForceUpdateFromAnyVersion(true);

<<<<<<< HEAD
            IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> deploymentOperation = packageManager.AddPackageByUriAsync(packageUri, options);
            deploymentOperation.get();

            // Check the status of the operation
=======
            auto deploymentOperation = packageManager.AddPackageByUriAsync(packageUri, options);
            deploymentOperation.get();

>>>>>>> 2978ce163a (dev)
            if (deploymentOperation.Status() == AsyncStatus::Error)
            {
                auto deploymentResult{ deploymentOperation.GetResults() };
                auto errorCode = deploymentOperation.ErrorCode();
                auto errorText = deploymentResult.ErrorText();
<<<<<<< HEAD

=======
>>>>>>> 2978ce163a (dev)
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
<<<<<<< HEAD

=======
>>>>>>> 2978ce163a (dev)
            return true;
        }
        catch (std::exception& e)
        {
            Logger::error("Exception thrown while trying to register package: {}", e.what());
<<<<<<< HEAD

=======
>>>>>>> 2978ce163a (dev)
            return false;
        }
    }

    bool UnRegisterPackage(const std::wstring& pkgDisplayName)
    {
        try
        {
            PackageManager packageManager;
<<<<<<< HEAD
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
=======
            const auto packages = packageManager.FindPackagesForUser({});

            bool any = false;
            for (auto const& pkg : packages)
            {
                const auto& fullName = std::wstring{ pkg.Id().FullName() };
                if (fullName.find(pkgDisplayName) != std::wstring::npos)
                {
                    any = true;
                    auto op = packageManager.RemovePackageAsync(fullName);
                    op.get();

                    if (op.Status() == AsyncStatus::Error)
                    {
                        auto res = op.GetResults();
                        auto code = op.ErrorCode();
                        auto text = res.ErrorText();
                        Logger::error(L"Unregister {} package failed. ErrorCode: {}, ErrorText: {}", fullName, std::to_wstring(code), text);
                        return false;
                    }
                    else if (op.Status() == AsyncStatus::Canceled)
                    {
                        Logger::error(L"Unregister {} package canceled.", fullName);
                        return false;
                    }
                    else if (op.Status() == AsyncStatus::Completed)
                    {
                        Logger::info(L"Unregister {} package completed.", fullName);
                    }
                    else
                    {
                        Logger::debug(L"Unregister {} package started.", fullName);
                    }
                }
            }
            // If nothing matched, treat as success (nothing to remove)
            return true;
>>>>>>> 2978ce163a (dev)
        }
        catch (std::exception& e)
        {
            Logger::error("Exception thrown while trying to unregister package: {}", e.what());
            return false;
        }
<<<<<<< HEAD

        return true;
=======
>>>>>>> 2978ce163a (dev)
    }

    std::vector<std::wstring> FindMsixFile(const std::wstring& directoryPath, bool recursive)
    {
<<<<<<< HEAD
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

=======
        std::vector<std::wstring> results;
>>>>>>> 2978ce163a (dev)
        try
        {
            if (recursive)
            {
                for (const auto& entry : std::filesystem::recursive_directory_iterator(directoryPath))
                {
<<<<<<< HEAD
                    if (entry.is_regular_file())
                    {
                        const auto& fileName = entry.path().filename().string();
                        if (std::regex_match(fileName, pattern))
                        {
                            matchedFiles.push_back(entry.path());
                        }
=======
                    if (!entry.is_regular_file()) continue;
                    auto ext = entry.path().extension().wstring();
                    std::transform(ext.begin(), ext.end(), ext.begin(), ::towlower);
                    if (ext == L".msix" || ext == L".msixbundle")
                    {
                        results.push_back(entry.path().wstring());
>>>>>>> 2978ce163a (dev)
                    }
                }
            }
            else
            {
                for (const auto& entry : std::filesystem::directory_iterator(directoryPath))
                {
<<<<<<< HEAD
                    if (entry.is_regular_file())
                    {
                        const auto& fileName = entry.path().filename().string();
                        if (std::regex_match(fileName, pattern))
                        {
                            matchedFiles.push_back(entry.path());
                        }
=======
                    if (!entry.is_regular_file()) continue;
                    auto ext = entry.path().extension().wstring();
                    std::transform(ext.begin(), ext.end(), ext.begin(), ::towlower);
                    if (ext == L".msix" || ext == L".msixbundle")
                    {
                        results.push_back(entry.path().wstring());
>>>>>>> 2978ce163a (dev)
                    }
                }
            }
        }
<<<<<<< HEAD
        catch (const std::exception& ex)
        {
            Logger::error("An error occurred while searching for MSIX files: " + std::string(ex.what()));
        }

        return matchedFiles;
=======
        catch (const std::exception& e)
        {
            Logger::error(L"FindMsixFile error: {}", winrt::to_hstring(e.what()));
        }
        return results;
>>>>>>> 2978ce163a (dev)
    }

    bool IsPackageSatisfied(const std::wstring& appxPath)
    {
        std::wstring targetName;
        PACKAGE_VERSION targetVersion{};
<<<<<<< HEAD

        if (!GetPackageNameAndVersionFromAppx(appxPath, targetName, targetVersion))
        {
            Logger::error(L"Failed to get package name and version from appx: " + appxPath);
=======
        if (!GetPackageNameAndVersionFromAppx(appxPath, targetName, targetVersion))
        {
>>>>>>> 2978ce163a (dev)
            return false;
        }

        PackageManager pm;
<<<<<<< HEAD

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
=======
        for (const auto& pkg : pm.FindPackagesForUser({}))
        {
            if (std::wstring{ pkg.Id().Name() } == targetName)
            {
                auto v = pkg.Id().Version();
                if (compare_versions(v, targetVersion) >= 0)
                {
                    Logger::info(L"Package {} is satisfied. Installed version {}.{}.{}.{} >= target {}.{}.{}.{}, appxPath: {}",
                                 targetName,
                                 v.Major, v.Minor, v.Build, v.Revision,
                                 targetVersion.Major, targetVersion.Minor, targetVersion.Build, targetVersion.Revision,
                                 appxPath);
                    return true;
                }
                break;
            }
        }

        Logger::info(L"Package {} is not satisfied. Target version: {}.{}.{}.{}; appxPath: {}",
                     targetName,
                     targetVersion.Major, targetVersion.Minor, targetVersion.Build, targetVersion.Revision,
                     appxPath);
>>>>>>> 2978ce163a (dev)
        return false;
    }

    bool RegisterPackage(std::wstring pkgPath, std::vector<std::wstring> dependencies)
    {
        try
        {
            Uri packageUri{ pkgPath };
<<<<<<< HEAD

            PackageManager packageManager;

            // Declare use of an external location
            DeploymentOptions options = DeploymentOptions::ForceTargetApplicationShutdown;

            Collections::IVector<Uri> uris = winrt::single_threaded_vector<Uri>();
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
=======
            PackageManager packageManager;

            DeploymentOptions options = DeploymentOptions::ForceTargetApplicationShutdown;
            Collections::IVector<Uri> uris = winrt::single_threaded_vector<Uri>();

            for (const auto& dep : dependencies)
            {
                try
                {
                    if (IsPackageSatisfied(dep))
                    {
                        Logger::info(L"Dependency already satisfied: {}", dep);
                    }
                    else
                    {
                        uris.Append(Uri(dep));
                    }
                }
                catch (const winrt::hresult_error& ex)
                {
                    Logger::error(L"Error creating Uri for dependency: %s", ex.message().c_str());
                }
            }

            auto op = packageManager.AddPackageAsync(packageUri, uris, options);
            op.get();

            if (op.Status() == AsyncStatus::Error)
            {
                auto res = op.GetResults();
                auto code = op.ErrorCode();
                auto text = res.ErrorText();
                Logger::error(L"Register {} package failed. ErrorCode: {}, ErrorText: {}", pkgPath, std::to_wstring(code), text);
                return false;
            }
            else if (op.Status() == AsyncStatus::Canceled)
>>>>>>> 2978ce163a (dev)
            {
                Logger::error(L"Register {} package canceled.", pkgPath);
                return false;
            }
<<<<<<< HEAD
            else if (deploymentOperation.Status() == AsyncStatus::Completed)
=======
            else if (op.Status() == AsyncStatus::Completed)
>>>>>>> 2978ce163a (dev)
            {
                Logger::info(L"Register {} package completed.", pkgPath);
            }
            else
            {
                Logger::debug(L"Register {} package started.", pkgPath);
            }
<<<<<<< HEAD
=======
            return true;
>>>>>>> 2978ce163a (dev)
        }
        catch (std::exception& e)
        {
            Logger::error("Exception thrown while trying to register package: {}", e.what());
<<<<<<< HEAD

            return false;
        }

        return true;
    }
}
=======
            return false;
        }
    }
}

>>>>>>> 2978ce163a (dev)
