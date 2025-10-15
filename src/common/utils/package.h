#pragma once

#include <Windows.h>

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

namespace package
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::ApplicationModel;
    using namespace winrt::Windows::Management::Deployment;
    using Microsoft::WRL::ComPtr;

    BOOL IsWin11OrGreater();

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
        explicit ComInitializer(DWORD coInitFlags = COINIT_MULTITHREADED);
        ~ComInitializer();
        bool Succeeded() const;

    private:
        bool _initialized;
    };

    bool GetPackageNameAndVersionFromAppx(
        const std::wstring& appxPath,
        std::wstring& outName,
        PACKAGE_VERSION& outVersion);

    std::optional<Package> GetRegisteredPackage(std::wstring packageDisplayName, bool checkVersion);

    bool IsPackageRegisteredWithPowerToysVersion(std::wstring packageDisplayName);

    bool RegisterSparsePackage(const std::wstring& externalLocation, const std::wstring& sparsePkgPath);

    bool UnRegisterPackage(const std::wstring& pkgDisplayName);

    std::vector<std::wstring> FindMsixFile(const std::wstring& directoryPath, bool recursive);

    bool IsPackageSatisfied(const std::wstring& appxPath);

    bool RegisterPackage(std::wstring pkgPath, std::vector<std::wstring> dependencies);
}
