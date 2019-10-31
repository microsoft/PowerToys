#include "stdafx.h"
#include "PowerRenameExt.h"
#include <PowerRenameUI.h>
#include <PowerRenameItem.h>
#include <PowerRenameManager.h>
#include <trace.h>
#include "resource.h"

extern HINSTANCE g_hInst;

struct InvokeStruct
{
    HWND hwndParent;
    IStream* pstrm;
};

const wchar_t powerRenameRegPath[] = L"Software\\Microsoft\\PowerRename";
const wchar_t powerRenameRegEnabledName[] = L"Enabled";

bool CPowerRenameMenu::IsEnabled()
{
    DWORD type = REG_DWORD;
    DWORD dwEnabled = 0;
    DWORD cb = sizeof(dwEnabled);
    SHGetValue(HKEY_CURRENT_USER, powerRenameRegPath, powerRenameRegEnabledName, &type, &dwEnabled, &cb);
    return (dwEnabled == 0) ? false : true;
}

bool CPowerRenameMenu::SetEnabled(_In_ bool enabled)
{
    DWORD dwEnabled = enabled ? 1 : 0;
    return SUCCEEDED(HRESULT_FROM_WIN32(SHSetValueW(HKEY_CURRENT_USER, powerRenameRegPath, powerRenameRegEnabledName, REG_DWORD, &dwEnabled, sizeof(dwEnabled))));
}

CPowerRenameMenu::CPowerRenameMenu()
{
    DllAddRef();
}

CPowerRenameMenu::~CPowerRenameMenu()
{
    m_spdo = nullptr;
    DllRelease();
}

HRESULT CPowerRenameMenu::s_CreateInstance(_In_opt_ IUnknown*, _In_ REFIID riid, _Outptr_ void **ppv)
{
    *ppv = nullptr;
    HRESULT hr = E_OUTOFMEMORY;
    CPowerRenameMenu *pprm = new CPowerRenameMenu();
    if (pprm)
    {
        hr = pprm->QueryInterface(riid, ppv);
        pprm->Release();
    }
    return hr;
}

// IShellExtInit
HRESULT CPowerRenameMenu::Initialize(_In_opt_ PCIDLIST_ABSOLUTE, _In_ IDataObject *pdtobj, HKEY)
{
    // Check if we have disabled ourselves
    if (!IsEnabled())
        return E_FAIL;

    // Cache the data object to be used later
    m_spdo = pdtobj;
    return S_OK;
}

// IContextMenu
HRESULT CPowerRenameMenu::QueryContextMenu(HMENU hMenu, UINT index, UINT uIDFirst, UINT, UINT uFlags)
{
    // Check if we have disabled ourselves
    if (!IsEnabled())
        return E_FAIL;

    HRESULT hr = E_UNEXPECTED;
    if (m_spdo && !(uFlags & (CMF_DEFAULTONLY | CMF_VERBSONLY | CMF_OPTIMIZEFORINVOKE)))
    {
        wchar_t menuName[64] = { 0 };
        LoadString(g_hInst, IDS_POWERRENAME, menuName, ARRAYSIZE(menuName));
        InsertMenu(hMenu, index, MF_STRING | MF_BYPOSITION, uIDFirst++, menuName);
        hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
    }

    return hr;
}

HRESULT CPowerRenameMenu::InvokeCommand(_In_ LPCMINVOKECOMMANDINFO pici)
{
    HRESULT hr = E_FAIL;

    if (IsEnabled() &
        (IS_INTRESOURCE(pici->lpVerb)) &&
        (LOWORD(pici->lpVerb) == 0))
    {
        Trace::Invoked();
        InvokeStruct* pInvokeData = new InvokeStruct;
        hr = pInvokeData ? S_OK : E_OUTOFMEMORY;
        if (SUCCEEDED(hr))
        {
            hr = CoMarshalInterThreadInterfaceInStream(__uuidof(m_spdo), m_spdo, &(pInvokeData->pstrm));
            if (SUCCEEDED(hr))
            {
                hr = SHCreateThread(s_PowerRenameUIThreadProc, pInvokeData, CTF_COINIT | CTF_PROCESS_REF, nullptr) ? S_OK : E_FAIL;
                if (FAILED(hr))
                {
                    pInvokeData->pstrm->Release(); // if we failed to create the thread, then we must release the stream
                }
            }

            if (FAILED(hr))
            {
                delete pInvokeData;
            }
        }
        Trace::InvokedRet(hr);
    }

    return hr;
}

DWORD WINAPI CPowerRenameMenu::s_PowerRenameUIThreadProc(_In_ void* pData)
{
    InvokeStruct* pInvokeData = static_cast<InvokeStruct*>(pData);
    CComPtr<IDataObject> spdo;
    HRESULT hr = CoGetInterfaceAndReleaseStream(pInvokeData->pstrm, IID_PPV_ARGS(&spdo));
    if (SUCCEEDED(hr))
    {
        // Create the smart rename manager
        CComPtr<IPowerRenameManager> spsrm;
        hr = CPowerRenameManager::s_CreateInstance(&spsrm);
        if (SUCCEEDED(hr))
        {
            // Create the factory for our items
            CComPtr<IPowerRenameItemFactory> spsrif;
            hr = CPowerRenameItem::s_CreateInstance(nullptr, IID_PPV_ARGS(&spsrif));
            if (SUCCEEDED(hr))
            {
                // Pass the factory to the manager
                hr = spsrm->put_smartRenameItemFactory(spsrif);
                if (SUCCEEDED(hr))
                {
                    // Create the smart rename UI instance and pass the smart rename manager
                    CComPtr<IPowerRenameUI> spsrui;
                    hr = CPowerRenameUI::s_CreateInstance(spsrm, spdo, false, &spsrui);
                    if (SUCCEEDED(hr))
                    {
                        // Call blocks until we are done
                        spsrui->Show(pInvokeData->hwndParent);
                        spsrui->Close();
                    }
                }
            }

            // Need to call shutdown to break circular dependencies
            spsrm->Shutdown();
        }
    }

    delete pInvokeData;

    Trace::UIShownRet(hr);

    return 0;
}
