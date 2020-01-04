#include "stdafx.h"

#include "resource.h"
#include <CLSID.h>
#include <PowerRenameExt.h>
#include <common.h>

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
        PostThreadMessage(main_thread_id, WM_QUIT, 0, 0);
    }
}
HINSTANCE g_hInst = 0;

class CPowerRenameClassLocalFactory : public IClassFactory
{
public:
    CPowerRenameClassLocalFactory(_In_ REFCLSID clsid) :
        _clsid(clsid)
    {
    }

    // IUnknown methods
    IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _COM_Outptr_ void** ppv)
    {
        static const QITAB qit[] = {
            QITABENT(CPowerRenameClassLocalFactory, IClassFactory),
            { 0 }
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG)
    AddRef()
    {
        return ++_refCount;
    }

    IFACEMETHODIMP_(ULONG)
    Release()
    {
        LONG refCount = --_refCount;
        return refCount;
    }

    // IClassFactory methods
    IFACEMETHODIMP CreateInstance(_In_opt_ IUnknown* punkOuter, _In_ REFIID riid, _Outptr_ void** ppv)
    {
        *ppv = NULL;
        HRESULT hr;
        if (punkOuter)
        {
            hr = CLASS_E_NOAGGREGATION;
        }
        else
        {
            if (_clsid == CLSID_PowerRenameMenu)
            {
                hr = CPowerRenameMenu::s_CreateInstance(punkOuter, riid, ppv);
            }
            else
            {
                hr = CLASS_E_CLASSNOTAVAILABLE;
            }
        }
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL)
    {
        return S_OK;
    }

private:
    std::atomic<long> _refCount;
    CLSID _clsid;
};

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                      _In_opt_ HINSTANCE,
                      _In_ LPWSTR lpCmdLine,
                      _In_ int nCmdShow)
{
    main_thread_id = GetCurrentThreadId();
    winrt::init_apartment();
    g_hInst = hInstance;
    auto factory = std::make_unique<CPowerRenameClassLocalFactory>(CLSID_PowerRenameMenu);
    DWORD token;
    if (!SUCCEEDED(CoRegisterClassObject(CLSID_PowerRenameMenu, factory.get(), CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE, &token)))
    {
        return 1;
    }

    // Run msg loop for the local COM server
    run_message_loop();

    CoRevokeClassObject(token);
    winrt::uninit_apartment();
    return 0;
}
