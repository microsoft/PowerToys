// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

// Additional libraries to link
#pragma comment(lib, "shlwapi")

#include "Registry.h"
#include "ClassFactory.h"

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
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}


STDAPI DllRegisterServer()
{
    if (!add_registry_keys())
    {
        // Best effort here
        delete_registry_keys();
        return E_FAIL;
    }

    return S_OK;
}

STDAPI DllUnregisterServer()
{
    return delete_registry_keys() ? S_OK : E_FAIL;
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

// Things to implement:
// 1. (DONE) A class which implements IExplorerCommand
// 2. (DONE) A class which implements IClassFactory
// 3. (DONE) DLL register/unregister functions which will create registry entries
// 4. (DONE) Other DLL exported functions
// 5. (DONE) Implement IShellExtInit in ExplorerCommand
// 6. (DONE) Implement IContextMenu in ExplorerCommand
// 7. (DONE) Extract useful functions from HandlesExperiment to a static library
// 8. (DONE) Implement IPC in Lib - to be used between UI and DLL
// 9. Implement UI
