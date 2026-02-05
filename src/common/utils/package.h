#pragma once

#include <Windows.h>

#include <algorithm>
#include <exception>
#include <filesystem>
#include <regex>
#include <string>
#include <optional>
#include <vector>

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

    // Implementations in package.cpp
    bool GetPackageNameAndVersionFromAppx(
        const std::wstring& appxPath,
        std::wstring& outName,
        PACKAGE_VERSION& outVersion);

    inline std::optional<Package> GetRegisteredPackage(std::wstring packageDisplayName, bool checkVersion)
    {
        PackageManager packageManager;

        for (const auto& package : packageManager.FindPackagesForUser({}))
        {
            const auto& packageFullName = std::wstring{ package.Id().FullName() };
            const auto& packageVersion = package.Id().Version();

            if (packageFullName.find(packageDisplayName) != std::wstring::npos)
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

    // Implementations in package.cpp
    bool RegisterSparsePackage(const std::wstring& externalLocation, const std::wstring& sparsePkgPath);
    bool UnRegisterPackage(const std::wstring& pkgDisplayName);
    std::vector<std::wstring> FindMsixFile(const std::wstring& directoryPath, bool recursive);
    bool IsPackageSatisfied(const std::wstring& appxPath);
    bool RegisterPackage(std::wstring pkgPath, std::vector<std::wstring> dependencies);
}
