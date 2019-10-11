#include "stdafx.h"
#include "PowerRenameExt.h"
#include <PowerRenameUI.h>
#include <PowerRenameItem.h>
#include <PowerRenameManager.h>
#include "resource.h"

extern HINSTANCE g_hInst;

HWND g_hwndParent = 0;


const wchar_t powerRenameRegPath[] = L"Softare\\Microsoft\\PowerRename";
const wchar_t powerRenameRegEnabledName[] = L"Enabled";

bool CPowerRenameMenu::IsEnabled()
{
    DWORD type = REG_DWORD;
    DWORD cb = 0;
    BOOL isEnabled = TRUE;
    SHGetValue(HKEY_CURRENT_USER, powerRenameRegPath, powerRenameRegEnabledName, &type, &isEnabled, &cb);
    return !!isEnabled;
}

bool CPowerRenameMenu::SetEnabled(_In_ bool enabled)
{
    return SUCCEEDED(HRESULT_FROM_WIN32(SHSetValueW(HKEY_CURRENT_USER, powerRenameRegPath, powerRenameRegEnabledName, REG_DWORD, &enabled, sizeof(enabled))));
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
    if (m_spdo)
    {
        if ((uFlags & ~CMF_OPTIMIZEFORINVOKE) && (uFlags & ~(CMF_DEFAULTONLY | CMF_VERBSONLY)))
        {
            wchar_t menuName[64] = { 0 };
            LoadString(g_hInst, IDS_POWERRENAME, menuName, ARRAYSIZE(menuName));
            InsertMenu(hMenu, index, MF_STRING | MF_BYPOSITION, uIDFirst++, menuName);
            hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        }
    }

    return hr;
}

HRESULT CPowerRenameMenu::InvokeCommand(_In_ LPCMINVOKECOMMANDINFO pici)
{
    // Check if we have disabled ourselves
    if (!IsEnabled())
        return E_FAIL;

    HRESULT hr = E_FAIL;

    if ((IS_INTRESOURCE(pici->lpVerb)) &&
        (LOWORD(pici->lpVerb) == 0))
    {
        IStream* pstrm = nullptr;
        if (SUCCEEDED(CoMarshalInterThreadInterfaceInStream(__uuidof(m_spdo), m_spdo, &pstrm)))
        {
            if (!SHCreateThread(s_PowerRenameUIThreadProc, pstrm, CTF_COINIT | CTF_PROCESS_REF, nullptr))
            {
                pstrm->Release(); // if we failed to create the thread, then we must release the stream
            }
        }
    }

    return hr;
}

DWORD WINAPI CPowerRenameMenu::s_PowerRenameUIThreadProc(_In_ void* pData)
{
    IStream* pstrm = static_cast<IStream*>(pData);
    CComPtr<IDataObject> spdo;
    if (SUCCEEDED(CoGetInterfaceAndReleaseStream(pstrm, IID_PPV_ARGS(&spdo))))
    {
        // Create the smart rename manager
        CComPtr<IPowerRenameManager> spsrm;
        if (SUCCEEDED(CPowerRenameManager::s_CreateInstance(&spsrm)))
        {
            // Create the factory for our items
            CComPtr<IPowerRenameItemFactory> spsrif;
            if (SUCCEEDED(CPowerRenameItem::s_CreateInstance(nullptr, IID_PPV_ARGS(&spsrif))))
            {
                // Pass the factory to the manager
                if (SUCCEEDED(spsrm->put_smartRenameItemFactory(spsrif)))
                {
                    // Create the smart rename UI instance and pass the smart rename manager
                    CComPtr<IPowerRenameUI> spsrui;
                    if (SUCCEEDED(CPowerRenameUI::s_CreateInstance(spsrm, spdo, false, &spsrui)))
                    {
                        // Call blocks until we are done
                        spsrui->Show();
                        spsrui->Close();
                    }
                }
            }

            // Need to call shutdown to break circular dependencies
            spsrm->Shutdown();
        }
    }

    return 0;
}
