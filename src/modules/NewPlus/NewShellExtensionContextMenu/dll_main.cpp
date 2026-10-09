#include "pch.h"

#include "shell_context_menu.h"
#include "dll_main.h"
#include "trace.h"

#include <common/Telemetry/EtwTrace/EtwTrace.h>
#include <common/utils/context_menu_lifecycle.h>

HMODULE module_instance_handle = 0;
Shared::Trace::ETWTrace trace(L"NewPlusShellExtension");
std::atomic_uint32_t active_rename_workers = 0;

namespace
{
    void ensure_servicing_window()
    {
        context_menu_lifecycle::ensure_servicing_window(
            &module_instance_handle,
            L"Microsoft.PowerToys.NewPlusContextMenu_",
            L"PowerToys.NewPlusContextMenu.ServicingWindow",
            20000,
            Trace::ServicingWindowInitialization);
    }
}

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
    ensure_servicing_window();
    return Module<ModuleType::InProc>::GetModule().GetActivationFactory(activatableClassId, factory);
}

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().GetObjectCount() == 0 && active_rename_workers.load() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _Outptr_ LPVOID FAR* ppv)
{
    ensure_servicing_window();
    return Module<InProc>::GetModule().GetClassObject(rclsid, riid, ppv);
}

CoCreatableClass(shell_context_menu)
