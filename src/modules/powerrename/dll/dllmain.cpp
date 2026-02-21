#include "pch.h"

#include "PowerRenameExt.h"
#include "../dll/RuntimeRegistration.h"

#include <trace.h>


#include <atomic>

std::atomic<DWORD> g_dwModuleRefCount = 0;
HINSTANCE g_hInst = 0;

class CPowerRenameClassFactory : public IClassFactory
{
public:
    CPowerRenameClassFactory(_In_ REFCLSID clsid) :
        m_refCount(1),
        m_clsid(clsid)
    {
        ModuleAddRef();
    }

    // IUnknown methods
    IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _COM_Outptr_ void** ppv)
    {
        static const QITAB qit[] = {
            QITABENT(CPowerRenameClassFactory, IClassFactory),
            { 0 }
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG)
    AddRef()
    {
        return ++m_refCount;
    }

    IFACEMETHODIMP_(ULONG)
    Release()
    {
        LONG refCount = --m_refCount;
        if (refCount == 0)
        {
            delete this;
        }
        return refCount;
    }

    // IClassFactory methods
    IFACEMETHODIMP CreateInstance(_In_opt_ IUnknown* punkOuter, _In_ REFIID riid, _Outptr_ void** ppv)
    {
        *ppv = NULL;
        HRESULT hr;
        if (punkOuter)
        {
            hr = CLASS_E_NOAGGREGATION;
        }
        else if (m_clsid == CLSID_PowerRenameMenu)
        {
            hr = CPowerRenameMenu::s_CreateInstance(punkOuter, riid, ppv);
        }
        else
        {
            hr = CLASS_E_CLASSNOTAVAILABLE;
        }
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL bLock)
    {
        if (bLock)
        {
            ModuleAddRef();
        }
        else
        {
            ModuleRelease();
        }
        return S_OK;
    }

private:
    ~CPowerRenameClassFactory()
    {
        ModuleRelease();
    }

    std::atomic<long> m_refCount;
    CLSID m_clsid;
};

BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, void*)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        g_hInst = hInstance;
        Trace::RegisterProvider();
        break;

    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

//
// Checks if there are any external references to this module
//
STDAPI DllCanUnloadNow(void)
{
    return (g_dwModuleRefCount == 0) ? S_OK : S_FALSE;
}

//
// DLL export for creating COM objects
//
STDAPI DllGetClassObject(_In_ REFCLSID clsid, _In_ REFIID riid, _Outptr_ void** ppv)
{
    HRESULT hr = E_FAIL;
    *ppv = NULL;
    CPowerRenameClassFactory* pClassFactory = new CPowerRenameClassFactory(clsid);
    hr = pClassFactory->QueryInterface(riid, ppv);
    pClassFactory->Release();
    return hr;
}

STDAPI DllRegisterServer()
{
    return S_OK;
}

STDAPI DllUnregisterServer()
{
    return S_OK;
}

void ModuleAddRef()
{
    g_dwModuleRefCount++;
}

void ModuleRelease()
{
    g_dwModuleRefCount--;
}

    // Update registration based on enabled state
EXTERN_C __declspec(dllexport) void UpdatePowerRenameRegistrationWin10(bool enabled)
{
    if (enabled)
    {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
        PowerRenameRuntimeRegistration::EnsureRegistered();
#endif
    }
    else
    {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
        PowerRenameRuntimeRegistration::Unregister();
#endif
    }
}
