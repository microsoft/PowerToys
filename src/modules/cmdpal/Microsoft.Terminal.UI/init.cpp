// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "pch.h"

#pragma warning(suppress : 26440) // Not interested in changing the specification of DllMain to make it noexcept given it's an interface to the OS.
BOOL WINAPI DllMain(HINSTANCE hInstDll, DWORD reason, LPVOID /*reserved*/)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hInstDll);
        break;
    case DLL_PROCESS_DETACH:
        break;
    default:
        break;
    }

    return TRUE;
}
