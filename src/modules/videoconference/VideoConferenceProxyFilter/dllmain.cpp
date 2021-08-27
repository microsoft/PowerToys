#include "VideoCaptureProxyFilter.h"

#include <winrt/base.h>

#include <wil/registry.h>
#include <cguid.h>

namespace
{
#if defined(_WIN64)
    class __declspec(uuid("{31AD75E9-8C3A-49C8-B9ED-5880D6B4A764}")) GUID_DECL_POWERTOYS_VCM;
#elif defined(_WIN32)
    class __declspec(uuid("{31AD75E9-8C3A-49C8-B9ED-5880D6B4A732}")) GUID_DECL_POWERTOYS_VCM;
#endif
    const GUID CLSID_POWERTOYS_VCM = __uuidof(GUID_DECL_POWERTOYS_VCM);

    const REGPINTYPES MEDIA_TYPES = { &MEDIATYPE_Video, &MEDIASUBTYPE_MJPG };

    const wchar_t FILTER_NAME[] = L"Output";
    const REGFILTERPINS PINS_REGISTRATION = {
        (wchar_t*)FILTER_NAME,
        false,
        true,
        false,
        false,
        &CLSID_NULL,
        nullptr,
        1,
        &MEDIA_TYPES
    };

    HINSTANCE DLLInstance{};
}

struct __declspec(uuid("9DCAF869-9C13-4BDF-BD0D-3592C5579DD6")) VideoCaptureProxyFilterFactory : winrt::implements<VideoCaptureProxyFilterFactory, IClassFactory>
{
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown*, REFIID riid, void** ppvObject) noexcept override
    {
        try
        {
            return winrt::make<VideoCaptureProxyFilter>()->QueryInterface(riid, ppvObject);
        }
        catch (...)
        {
            return winrt::to_hresult();
        }
    }

    HRESULT STDMETHODCALLTYPE LockServer(BOOL fLock) noexcept override
    {
        if (fLock)
        {
            ++winrt::get_module_lock();
        }
        else
        {
            --winrt::get_module_lock();
        }

        return S_OK;
    }
};

HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
{
    if (winrt::get_module_lock())
    {
        return S_FALSE;
    }

    winrt::clear_factory_cache();
    return S_OK;
}

HRESULT STDMETHODCALLTYPE DllGetClassObject(GUID const& clsid, GUID const& iid, void** result)
{
    if (!result)
    {
        return E_POINTER;
    }

    if (iid != IID_IClassFactory && iid != IID_IUnknown)
    {
        return E_NOINTERFACE;
    }

    if (clsid != CLSID_POWERTOYS_VCM)
    {
        return E_INVALIDARG;
    }

    try
    {
        *result = nullptr;

        auto factory = winrt::make<VideoCaptureProxyFilterFactory>();
        factory->AddRef();
        *result = static_cast<void*>(factory.get());
        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }
}

std::wstring RegistryPath()
{
    std::wstring registryPath;
    registryPath.resize(CHARS_IN_GUID, L'\0');

    StringFromGUID2(CLSID_POWERTOYS_VCM, registryPath.data(), CHARS_IN_GUID);
    registryPath.resize(registryPath.size() - 1);
    registryPath = L"CLSID\\" + registryPath;
    return registryPath;
}

bool RegisterServer()
{
    std::wstring dllPath;
    dllPath.resize(MAX_PATH, L'\0');
    if (auto length = GetModuleFileNameW(DLLInstance, dllPath.data(), MAX_PATH); length != 0)
    {
        dllPath.resize(length);
    }
    else
    {
        return false;
    }

    wil::unique_hkey key;
    wil::unique_hkey subkey;
    const auto registryPath = RegistryPath();
    if (RegCreateKeyW(HKEY_CLASSES_ROOT, registryPath.c_str(), &key))
    {
        return false;
    }

    if (RegSetValueW(key.get(), nullptr, REG_SZ, CAMERA_NAME, sizeof(CAMERA_NAME)))
    {
        return false;
    }

    if (RegCreateKeyW(key.get(), L"InprocServer32", &subkey))
    {
        return false;
    }

    if (RegSetValueW(subkey.get(), nullptr, REG_SZ, dllPath.c_str(), static_cast<DWORD>((dllPath.length() + 1) * sizeof(wchar_t))))
    {
        return false;
    }
    const wchar_t THREADING_MODEL[] = L"Both";
    RegSetValueExW(subkey.get(), L"ThreadingModel", 0, REG_SZ, (const BYTE*)THREADING_MODEL, sizeof(THREADING_MODEL));

    return true;
}

bool UnregisterServer()
{
    const auto registryPath = RegistryPath();
    return !RegDeleteTreeW(HKEY_CLASSES_ROOT, registryPath.c_str());
}

bool RegisterFilter()
{
    auto filterMapper = wil::CoCreateInstanceNoThrow<IFilterMapper2>(CLSID_FilterMapper2);
    if (!filterMapper)
    {
        return false;
    }

    REGFILTER2 regFilter{ .dwVersion = 1, .dwMerit = MERIT_DO_NOT_USE, .cPins = 1, .rgPins = &PINS_REGISTRATION };

    wil::com_ptr_nothrow<IMoniker> moniker;

    return SUCCEEDED(filterMapper->RegisterFilter(
        CLSID_POWERTOYS_VCM, CAMERA_NAME, &moniker, &CLSID_VideoInputDeviceCategory, nullptr, &regFilter));
}

bool UnregisterFilter()
{
    auto filterMapper = wil::CoCreateInstanceNoThrow<IFilterMapper2>(CLSID_FilterMapper2);
    if (!filterMapper)
    {
        return false;
    }

    return SUCCEEDED(filterMapper->UnregisterFilter(&CLSID_VideoInputDeviceCategory, nullptr, CLSID_POWERTOYS_VCM));
}

HRESULT STDMETHODCALLTYPE DllRegisterServer()
{
    if (!RegisterServer())
    {
        UnregisterServer();
        return E_FAIL;
    }

    auto COMContext = wil::CoInitializeEx(COINIT_APARTMENTTHREADED);

    if (!RegisterFilter())
    {
        UnregisterFilter();
        UnregisterServer();
        return E_FAIL;
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE DllUnregisterServer()
{
    auto COMContext = wil::CoInitializeEx(COINIT_APARTMENTTHREADED);

    UnregisterFilter();
    UnregisterServer();

    return S_OK;
}

HRESULT STDMETHODCALLTYPE DllInstall(BOOL install, LPCWSTR)
{
    if (install)
    {
        return DllRegisterServer();
    }
    else
    {
        return DllUnregisterServer();
    }
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID)
{
    if (fdwReason == DLL_PROCESS_ATTACH)
    {
        DLLInstance = hinstDLL;
    }

    return true;
}
