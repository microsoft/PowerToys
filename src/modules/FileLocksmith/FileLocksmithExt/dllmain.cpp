// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

// Additional libraries to link
#pragma comment(lib, "shlwapi")

#include "ClassFactory.h"
#include "Trace.h"

namespace globals
{
    HMODULE instance;
    std::atomic<ULONG> ref_count;
    std::atomic<bool> enabled;
}

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        globals::instance = hModule;
        Trace::RegisterProvider();
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

STDAPI DllRegisterServer()
{
    return S_OK;
}

STDAPI DllUnregisterServer()
{
    return S_OK;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    HRESULT result = E_FAIL;
    *ppv = NULL;
    ClassFactory* class_factory = new (std::nothrow) ClassFactory(clsid);
    if (class_factory)
    {
        result = class_factory->QueryInterface(riid, ppv);
        class_factory->Release();
    }

    return result;
}

STDAPI DllCanUnloadNow(void)
{
    return globals::ref_count == 0 ? S_OK : S_FALSE;
}
