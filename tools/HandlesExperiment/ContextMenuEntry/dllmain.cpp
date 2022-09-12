// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

// Additional libraries to link
#pragma comment(lib, "shlwapi")

#include "Registry.h"

HMODULE dll_instance;

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        dll_instance = hModule;
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

// Things to implement:
// 1. (DONE) A class which implements IExplorerCommand
// 2. (DONE) A class which implements IClassFactory
// 3. (DONE) DLL register/unregister functions which will create registry entries
// 4. Other DLL exported functions
