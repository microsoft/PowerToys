#include "stdafx.h"

#include "resource.h"
#include <CLSID.h>
#include <PowerRenameExt.h>
#include <common.h>
#include <com_object_factory.h>

std::atomic<DWORD> g_dwModuleRefCount = 0;

DWORD main_thread_id;

void ModuleAddRef()
{
    ++g_dwModuleRefCount;
}

void ModuleRelease()
{
    if (--g_dwModuleRefCount == 0)
    {
        // Do nothing and keep the COM server in memory forever. We might want to introduce delayed shutdown and/or
        // periodic polling whether a user has disabled us in settings. Tracking this in #1217
    }
}
HINSTANCE g_hInst = 0;


int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                      _In_opt_ HINSTANCE,
                      _In_ LPWSTR lpCmdLine,
                      _In_ int nCmdShow)
{
    main_thread_id = GetCurrentThreadId();
    winrt::init_apartment();
    g_hInst = hInstance;
    com_object_factory<CPowerRenameMenu> factory;
    DWORD token;
    if (!SUCCEEDED(CoRegisterClassObject(CLSID_PowerRenameMenu, &factory, CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE, &token)))
    {
        return 1;
    }

    // Run msg loop for the local COM server
    run_message_loop();

    CoRevokeClassObject(token);
    winrt::uninit_apartment();
    return 0;
}
