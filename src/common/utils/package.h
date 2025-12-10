#pragma once

#include <Windows.h>

#include <appxpackaging.h>
#include <exception>
#include <string>
#include <Shlwapi.h>
#include <wrl/client.h>


#include "../logger/logger.h"

using namespace winrt::Windows::Foundation;
using Microsoft::WRL::ComPtr;

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

__declspec(dllexport) EXTERN_C bool GetPackageNameAndVersionFromAppx(
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
