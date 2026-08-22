#pragma once

#include <Windows.h>

#include <string>

#include "Package.g.h"


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
namespace winrt::PowerToys::Interop::implementation
{

    struct Package : PackageT<Package>
    {
    public:
        static bool GetPackageNameAndVersionFromAppx(
            winrt::hstring const& appxPath,
            winrt::hstring& outName,
            winrt::PowerToys::Interop::PACKAGE_VERSION& outVersion);
    };
}

namespace winrt::PowerToys::Interop::factory_implementation
{
    struct Package : PackageT<Package, implementation::Package>
    {
    };
}

