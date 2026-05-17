#include "pch.h"

#pragma comment(lib, "shlwapi")

#include "dllmain.h"

namespace globals
{
    HMODULE instance;
}

BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD   ul_reason_for_call,
                      LPVOID  /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        globals::instance = hModule;
        break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
