// PowerRenameTest.cpp : Defines the entry point for the application.
//

#include "stdafx.h"
#include "PowerRenameTest.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameItem.h>
#include <PowerRenameUI.h>
#include <PowerRenameManager.h>
#include <Shobjidl.h>

#pragma comment(linker,"/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

HINSTANCE g_hInst;

// {0440049F-D1DC-4E46-B27B-98393D79486B}
//DEFINE_GUID(CLSID_PowerRenameMenu, 0x0440049F, 0xD1DC, 0x4E46, 0xB2, 0x7B, 0x98, 0x39, 0x3D, 0x79, 0x48, 0x6B);

class __declspec(uuid("{0440049F-D1DC-4E46-B27B-98393D79486B}")) Foo;
static const CLSID CLSID_PowerRenameMenu = __uuidof(Foo);

DEFINE_GUID(BHID_DataObject, 0xb8c0bd9f, 0xed24, 0x455c, 0x83, 0xe6, 0xd5, 0x39, 0xc, 0x4f, 0xe8, 0xc4);

int APIENTRY wWinMain(
    _In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ PWSTR lpCmdLine,
    _In_ int nCmdShow)
{
    g_hInst = hInstance;
    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
    if (SUCCEEDED(hr))
    {
        // Create the rename manager
        CComPtr<IPowerRenameManager> spsrm;
        if (SUCCEEDED(CPowerRenameManager::s_CreateInstance(&spsrm)))
        {
            // Create the factory for our items
            CComPtr<IPowerRenameItemFactory> spsrif;
            if (SUCCEEDED(CPowerRenameItem::s_CreateInstance(nullptr, IID_PPV_ARGS(&spsrif))))
            {
                // Pass the factory to the manager
                if (SUCCEEDED(spsrm->put_renameItemFactory(spsrif)))
                {
                    // Create the rename UI instance and pass the manager
                    CComPtr<IPowerRenameUI> spsrui;
                    if (SUCCEEDED(CPowerRenameUI::s_CreateInstance(spsrm, nullptr, true, &spsrui)))
                    {
                        // Call blocks until we are done
                        spsrui->Show(NULL);
                        spsrui->Close();

                        // Need to call shutdown to break circular dependencies
                        spsrm->Shutdown();
                    }
                }
            }
        }
        CoUninitialize();
    }
    return 0;
}