#include "pch.h"
#include "dllmain.h"

#include <trace.h>

CImageResizerExtModule _AtlModule;
HINSTANCE g_hInst_imageResizer = 0;

extern "C" BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        g_hInst_imageResizer = hInstance;
        Trace::RegisterProvider();
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return _AtlModule.DllMain(dwReason, lpReserved);
}

    // Update registration based on enabled state
EXTERN_C __declspec(dllexport) void UpdateImageResizerRegistrationWin10(bool enabled)
{
    if (enabled)
    {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
        ImageResizerRuntimeRegistration::EnsureRegistered();
#endif
    }
    else
    {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
        ImageResizerRuntimeRegistration::Unregister();
#endif
    }
}
