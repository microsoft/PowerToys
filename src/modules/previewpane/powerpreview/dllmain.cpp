#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include "powerpreview.h"

// F654F1BF-54D9-4A2E-B703-889091D3CB2D
const CLSID CLSID_SHIMActivateSvgPreviewHandler = { 0xF654F1BF, 0x54D9, 0x4A2E, { 0xB7, 0x03, 0x88, 0x90, 0x91, 0xD3, 0xCB, 0x2D } };

// ddee2b8a-6807-48a6-bb20-2338174ff779
const CLSID CLSID_SvgPreviewHandler = { 0xddee2b8a, 0x6807, 0x48a6, { 0xbb, 0x20, 0x23, 0x38, 0x17, 0x4f, 0xf7, 0x79 } };

// E0907A95-6F9A-4D1B-A97A-7D9D2648881E
const CLSID CLSID_SHIMActivateMdPreviewHandler = { 0xE0907A95, 0x6F9A, 0x4D1B, { 0xA9, 0x7A, 0x7D, 0x9D, 0x26, 0x48, 0x88, 0x1E } };

// 45769bcc-e8fd-42d0-947e-02beef77a1f5
const CLSID CLSID_MdPreviewHandler = { 0x45769bcc, 0xe8fd, 0x42d0, { 0x94, 0x7e, 0x02, 0xbe, 0xef, 0x77, 0xa1, 0xf5 } };

HRESULT CALLBACK DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    *ppv = NULL;
    HRESULT hr = S_OK;

    if (clsid == CLSID_SHIMActivateSvgPreviewHandler)
    {
        hr = CoGetClassObject(CLSID_SvgPreviewHandler, CLSCTX_INPROC_SERVER, NULL, riid, ppv);
    }
    else if (clsid == CLSID_SHIMActivateMdPreviewHandler)
    {
        hr = CoGetClassObject(CLSID_MdPreviewHandler, CLSCTX_INPROC_SERVER, NULL, riid, ppv);
    }

    return hr;
}

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
    return new PowerPreviewModule();
}
