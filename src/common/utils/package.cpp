#include "pch.h"
#include "package.h"
#include <common/utils/winapi_error.h>

namespace package
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::ApplicationModel;
    using namespace winrt::Windows::Management::Deployment;

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
    {
        try
        {
            ComInitializer comInit;
            if (!comInit.Succeeded())
            {
                Logger::error(L"COM initialization failed.");
                return false;
            }

            Microsoft::WRL::ComPtr<IAppxFactory> factory;
            Microsoft::WRL::ComPtr<IStream> stream;
            Microsoft::WRL::ComPtr<IAppxPackageReader> reader;
            Microsoft::WRL::ComPtr<IAppxManifestReader> manifest;
            Microsoft::WRL::ComPtr<IAppxManifestPackageId> packageId;

            HRESULT hr = CoCreateInstance(__uuidof(AppxFactory), nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&factory));
            if (FAILED(hr))
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

            LPWSTR name = nullptr;
            hr = packageId->GetName(&name);
            if (FAILED(hr))
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

            outName = std::wstring(name);
            CoTaskMemFree(name);

            outVersion.Major = static_cast<UINT16>((ver64 >> 48) & 0xFFFF);
            outVersion.Minor = static_cast<UINT16>((ver64 >> 32) & 0xFFFF);
            outVersion.Build = static_cast<UINT16>((ver64 >> 16) & 0xFFFF);
            outVersion.Revision = static_cast<UINT16>(ver64 & 0xFFFF);

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

    bool RegisterSparsePackage(const std::wstring& externalLocation, const std::wstring& sparsePkgPath)
    {
        try
        {
            Uri externalUri{ externalLocation };
            Uri packageUri{ sparsePkgPath };

            PackageManager packageManager;
            AddPackageOptions options;
            options.ExternalLocationUri(externalUri);
            options.ForceUpdateFromAnyVersion(true);

            auto deploymentOperation = packageManager.AddPackageByUriAsync(packageUri, options);
            deploymentOperation.get();

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

    bool UnRegisterPackage(const std::wstring& pkgDisplayName)
    {
        try
        {
            PackageManager packageManager;
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
        }
        catch (std::exception& e)
        {
            Logger::error("Exception thrown while trying to unregister package: {}", e.what());
            return false;
        }
    }

    std::vector<std::wstring> FindMsixFile(const std::wstring& directoryPath, bool recursive)
    {
        std::vector<std::wstring> results;
        try
        {
            if (recursive)
            {
                for (const auto& entry : std::filesystem::recursive_directory_iterator(directoryPath))
                {
                    if (!entry.is_regular_file()) continue;
                    auto ext = entry.path().extension().wstring();
                    std::transform(ext.begin(), ext.end(), ext.begin(), ::towlower);
                    if (ext == L".msix" || ext == L".msixbundle")
                    {
                        results.push_back(entry.path().wstring());
                    }
                }
            }
            else
            {
                for (const auto& entry : std::filesystem::directory_iterator(directoryPath))
                {
                    if (!entry.is_regular_file()) continue;
                    auto ext = entry.path().extension().wstring();
                    std::transform(ext.begin(), ext.end(), ext.begin(), ::towlower);
                    if (ext == L".msix" || ext == L".msixbundle")
                    {
                        results.push_back(entry.path().wstring());
                    }
                }
            }
        }
        catch (const std::exception& e)
        {
            Logger::error(L"FindMsixFile error: {}", winrt::to_hstring(e.what()));
        }
        return results;
    }

    bool IsPackageSatisfied(const std::wstring& appxPath)
    {
        std::wstring targetName;
        PACKAGE_VERSION targetVersion{};
        if (!GetPackageNameAndVersionFromAppx(appxPath, targetName, targetVersion))
        {
            return false;
        }

        PackageManager pm;
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
        return false;
    }

    bool RegisterPackage(std::wstring pkgPath, std::vector<std::wstring> dependencies)
    {
        try
        {
            Uri packageUri{ pkgPath };
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
            {
                Logger::error(L"Register {} package canceled.", pkgPath);
                return false;
            }
            else if (op.Status() == AsyncStatus::Completed)
            {
                Logger::info(L"Register {} package completed.", pkgPath);
            }
            else
            {
                Logger::debug(L"Register {} package started.", pkgPath);
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

