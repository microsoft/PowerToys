#include "pch.h"
#include "package.h"
#include "Package.g.cpp"
#include <Windows.h>

#include <appxpackaging.h>
#include <exception>
#include <string>
#include <Shlwapi.h>
#include <wrl/client.h>

using Microsoft::WRL::ComPtr;

namespace winrt::PowerToys::Interop::implementation
{
    bool Package::GetPackageNameAndVersionFromAppx(
        winrt::hstring const& appxPath,
        winrt::hstring& outName,
        winrt::PowerToys::Interop::PACKAGE_VERSION& outVersion)
    {
        try
        {
            ComInitializer comInit;
            if (!comInit.Succeeded())
            {
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

            outName = winrt::hstring{ name };
            CoTaskMemFree(name);

            outVersion.Major = static_cast<UINT16>((version >> 48) & 0xFFFF);
            outVersion.Minor = static_cast<UINT16>((version >> 32) & 0xFFFF);
            outVersion.Build = static_cast<UINT16>((version >> 16) & 0xFFFF);
            outVersion.Revision = static_cast<UINT16>(version & 0xFFFF);

            return true;
        }
        catch (const std::exception&)
        {
            return false;
        }
        catch (...)
        {
            return false;
        }
    }
}

