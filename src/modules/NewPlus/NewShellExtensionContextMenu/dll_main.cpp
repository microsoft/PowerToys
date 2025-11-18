#include "pch.h"

#include "shell_context_menu.h"
#include "dll_main.h"
#include "trace.h"

#include <common/Telemetry/EtwTrace/EtwTrace.h>

HMODULE module_instance_handle = 0;
Shared::Trace::ETWTrace trace(L"NewPlusShellExtension");

BOOL APIENTRY DllMain(HMODULE module_handle, DWORD ul_reason_for_call, LPVOID reserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        module_instance_handle = module_handle;
        Trace::RegisterProvider();
        newplus::utilities::init_logger();
        break;

    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

STDAPI DllGetActivationFactory(_In_ HSTRING activatableClassId, _COM_Outptr_ IActivationFactory** factory)
{
    return Module<ModuleType::InProc>::GetModule().GetActivationFactory(activatableClassId, factory);
}

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _Outptr_ LPVOID FAR* ppv)
{
    return Module<InProc>::GetModule().GetClassObject(rclsid, riid, ppv);
}

CoCreatableClass(shell_context_menu)
