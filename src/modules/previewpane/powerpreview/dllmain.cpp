#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include "powerpreview.h"
#include "CLSID.h"

// Logic to shim the Activation of .Net Assembly by calling CoGetClassObject. CLSID's of Preview Handlers should be present in the registry.dat under /Classes/CLSID/{guid}.
// See the existing Preview Handlers registry entry in registry.reg file.
// This is required since MSIX currently not support .Net Assembly for Com Activation for Preview Handlers.
HRESULT CALLBACK DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    *ppv = NULL;
    HRESULT hr = CLASS_E_CLASSNOTAVAILABLE;

    for (auto handler : NativeToManagedClsid)
    {
        if (handler.first == clsid)
        {
            hr = CoGetClassObject(handler.second, CLSCTX_INPROC_SERVER, NULL, riid, ppv);
            break;
        }
    }

    // In case of failed error code return by CoGetClassObject return CLASS_E_CLASSNOTAVAILABLE to the caller.
    if (FAILED(hr))
    {
        hr = CLASS_E_CLASSNOTAVAILABLE;
        *ppv = NULL;
    }

    return hr;
}

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}


extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerPreviewModule();
}
