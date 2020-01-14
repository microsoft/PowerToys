#include "stdafx.h"
#include "PowerRenameExt.h"
#include <PowerRenameUI.h>
#include <PowerRenameItem.h>
#include <PowerRenameManager.h>
#include <trace.h>
#include <common/common.h>
#include <Helpers.h>
#include <icon_helpers.h>
#include <Settings.h>
#include "resource.h"

extern HINSTANCE g_hInst;

struct InvokeStruct
{
    HWND hwndParent;
    IStream* pstrm;
};

CPowerRenameMenu::CPowerRenameMenu()
{
    ModuleAddRef();
}

CPowerRenameMenu::~CPowerRenameMenu()
{
    m_spdo = nullptr;
    DeleteObject(m_hbmpIcon);
    ModuleRelease();
}

HRESULT CPowerRenameMenu::s_CreateInstance(_In_opt_ IUnknown*, _In_ REFIID riid, _Outptr_ void** ppv)
{
    *ppv = nullptr;
    HRESULT hr = E_OUTOFMEMORY;
    CPowerRenameMenu* pprm = new CPowerRenameMenu();
    if (pprm)
    {
        hr = pprm->QueryInterface(riid, ppv);
        pprm->Release();
    }
    return hr;
}

// IShellExtInit
HRESULT CPowerRenameMenu::Initialize(_In_opt_ PCIDLIST_ABSOLUTE, _In_ IDataObject* pdtobj, HKEY)
{
    // Check if we have disabled ourselves
    if (!CSettings::GetEnabled())
        return E_FAIL;

    // Cache the data object to be used later
    m_spdo = pdtobj;
    return S_OK;
}

// IContextMenu
HRESULT CPowerRenameMenu::QueryContextMenu(HMENU hMenu, UINT index, UINT uIDFirst, UINT, UINT uFlags)
{
    // Check if we have disabled ourselves
    if (!CSettings::GetEnabled())
        return E_FAIL;

    // Check if we should only be on the extended context menu
    if (CSettings::GetExtendedContextMenuOnly() && (!(uFlags & CMF_EXTENDEDVERBS)))
        return E_FAIL;

    HRESULT hr = E_UNEXPECTED;
    if (m_spdo && !(uFlags & (CMF_DEFAULTONLY | CMF_VERBSONLY | CMF_OPTIMIZEFORINVOKE)))
    {
        wchar_t menuName[64] = { 0 };
        LoadString(g_hInst, IDS_POWERRENAME, menuName, ARRAYSIZE(menuName));

        MENUITEMINFO mii;
        mii.cbSize = sizeof(MENUITEMINFO);
        mii.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_STATE;
        mii.wID = uIDFirst++;
        mii.fType = MFT_STRING;
        mii.dwTypeData = (PWSTR)menuName;
        mii.fState = MFS_ENABLED;

        if (CSettings::GetShowIconOnMenu())
        {
            HICON hIcon = (HICON)LoadImage(g_hInst, MAKEINTRESOURCE(IDI_RENAME), IMAGE_ICON, 16, 16, 0);
            if (hIcon)
            {
                mii.fMask |= MIIM_BITMAP;
                if (m_hbmpIcon == NULL)
                {
                    m_hbmpIcon = CreateBitmapFromIcon(hIcon);
                }
                mii.hbmpItem = m_hbmpIcon;
                DestroyIcon(hIcon);
            }
        }

        if (!InsertMenuItem(hMenu, index, TRUE, &mii))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
        }
        else
        {
            hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        }
    }

    return hr;
}

HRESULT CPowerRenameMenu::InvokeCommand(_In_ LPCMINVOKECOMMANDINFO pici)
{
    HRESULT hr = E_FAIL;

    if (CSettings::GetEnabled() &&
        (IS_INTRESOURCE(pici->lpVerb)) &&
        (LOWORD(pici->lpVerb) == 0))
    {
        Trace::Invoked();
        InvokeStruct* pInvokeData = new (std::nothrow) InvokeStruct;
        hr = pInvokeData ? S_OK : E_OUTOFMEMORY;
        if (SUCCEEDED(hr))
        {
            pInvokeData->hwndParent = pici->hwnd;
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
    CComPtr<IUnknown> dataSource;
    HRESULT hr = CoGetInterfaceAndReleaseStream(pInvokeData->pstrm, IID_PPV_ARGS(&dataSource));
    if (SUCCEEDED(hr))
    {
        // Create the rename manager
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
                hr = spsrm->put_renameItemFactory(spsrif);
                if (SUCCEEDED(hr))
                {
                    // Create the rename UI instance and pass the rename manager
                    CComPtr<IPowerRenameUI> spsrui;
                    hr = CPowerRenameUI::s_CreateInstance(spsrm, dataSource, false, &spsrui);

                    if (SUCCEEDED(hr))
                    {
                        IDataObject* dummy;
                        // If we're running on a local COM server, we need to decrement module refcount, which was previously incremented in CPowerRenameMenu::Invoke.
                        if (SUCCEEDED(dataSource->QueryInterface(IID_IShellItemArray, reinterpret_cast<void**>(&dummy))))
                        {
                            ModuleRelease();
                        }
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

HRESULT __stdcall CPowerRenameMenu::GetTitle(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszName)
{
    app_name = GET_RES_STRING_WCHAR(IDS_POWERRENAME);
    return SHStrDup(app_name, ppszName);
}

HRESULT __stdcall CPowerRenameMenu::GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon)
{
    if (!CSettings::GetShowIconOnMenu())
    {
        *ppszIcon = nullptr;
        return E_NOTIMPL;
    }

    std::wstring iconResourcePath = get_module_filename();
    iconResourcePath += L",-";
    iconResourcePath += std::to_wstring(IDI_RENAME);
    return SHStrDup(iconResourcePath.c_str(), ppszIcon);
}

HRESULT __stdcall CPowerRenameMenu::GetToolTip(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszInfotip)
{
    *ppszInfotip = nullptr;
    return E_NOTIMPL;
}

HRESULT __stdcall CPowerRenameMenu::GetCanonicalName(GUID* pguidCommandName)
{
    *pguidCommandName = __uuidof(this);
    return S_OK;
}

HRESULT __stdcall CPowerRenameMenu::GetState(IShellItemArray* psiItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState)
{
    *pCmdState = CSettings::GetEnabled() ? ECS_ENABLED : ECS_HIDDEN;
    return S_OK;
}

//#define DEBUG_TELL_PID

HRESULT __stdcall CPowerRenameMenu::Invoke(IShellItemArray* psiItemArray, IBindCtx* /*pbc*/)
{
#if defined(DEBUG_TELL_PID)
    wchar_t buffer[256];
    swprintf_s(buffer, L"%d", GetCurrentProcessId());
    MessageBoxW(nullptr, buffer, L"PID", MB_OK);
#endif
    Trace::Invoked();
    InvokeStruct* pInvokeData = new (std::nothrow) InvokeStruct;
    HRESULT hr = pInvokeData ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
    {
        pInvokeData->hwndParent = nullptr;
        hr = CoMarshalInterThreadInterfaceInStream(__uuidof(psiItemArray), psiItemArray, &(pInvokeData->pstrm));
        if (!SUCCEEDED(hr))
        {
            return E_FAIL;
        }
        // Prevent Shutting down before PowerRenameUI is created
        ModuleAddRef();
        hr = SHCreateThread(s_PowerRenameUIThreadProc, pInvokeData, CTF_COINIT | CTF_PROCESS_REF, nullptr) ? S_OK : E_FAIL;
    }
    Trace::InvokedRet(hr);
    return S_OK;
}

HRESULT __stdcall CPowerRenameMenu::GetFlags(EXPCMDFLAGS* pFlags)
{
    *pFlags = ECF_DEFAULT;
    return S_OK;
}

HRESULT __stdcall CPowerRenameMenu::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    *ppEnum = nullptr;
    return E_NOTIMPL;
}
