// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include "VideoConferenceModule.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
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
    if (!instance)
    {
        instance = new VideoConferenceModule();
        return instance;
    }
    else
    {
        return nullptr;
    }
}
