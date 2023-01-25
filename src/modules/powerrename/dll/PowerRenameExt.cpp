#include "pch.h"
#include "PowerRenameExt.h"
#include <trace.h>
#include <Helpers.h>
#include <common/themes/icon_helpers.h>
#include <Settings.h>
#include "Generated Files/resource.h"

#include <common/utils/HDropIterator.h>
#include <common/utils/resources.h>
#include <common/utils/package.h>
#include <common/utils/process_path.h>

extern HINSTANCE g_hInst;

struct InvokeStruct
{
    HWND hwndParent;
    IStream* pstrm;
};

CPowerRenameMenu::CPowerRenameMenu()
{
    ModuleAddRef();
    app_name = GET_RESOURCE_STRING(IDS_POWERRENAME_APP_NAME);
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
    if (!CSettingsInstance().GetEnabled())
        return E_FAIL;

    // Cache the data object to be used later
    m_spdo = pdtobj;
    return S_OK;
}

// IContextMenu
HRESULT CPowerRenameMenu::QueryContextMenu(HMENU hMenu, UINT index, UINT uIDFirst, UINT, UINT uFlags)
{
    // Check if we have disabled ourselves
    if (!CSettingsInstance().GetEnabled())
        return E_FAIL;

    // Check if at least one of the selected items is actually renamable.
    if (!DataObjectContainsRenamableItem(m_spdo))
        return E_FAIL;

    // Check if we should only be on the extended context menu
    if (CSettingsInstance().GetExtendedContextMenuOnly() && (!(uFlags & CMF_EXTENDEDVERBS)))
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

        if (CSettingsInstance().GetShowIconOnMenu())
        {
            HICON hIcon = static_cast<HICON>(LoadImage(g_hInst, MAKEINTRESOURCE(IDI_RENAME), IMAGE_ICON, 16, 16, 0));
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
    return RunPowerRename(pici, nullptr);
}

HRESULT CPowerRenameMenu::RunPowerRename(CMINVOKECOMMANDINFO* pici, IShellItemArray* psiItemArray)
{
    HRESULT hr = E_FAIL;

    if (CSettingsInstance().GetEnabled() &&
        (IS_INTRESOURCE(pici->lpVerb)) &&
        (LOWORD(pici->lpVerb) == 0))
    {
        Trace::Invoked();
        // Set the application path based on the location of the dll
        std::wstring path = get_module_folderpath(g_hInst);
        path = path + L"\\PowerToys.PowerRename.exe";
        LPTSTR lpApplicationName = path.data();
        // Create an anonymous pipe to stream filenames
        SECURITY_ATTRIBUTES sa;
        HANDLE hReadPipe;
        HANDLE hWritePipe;
        sa.nLength = sizeof(SECURITY_ATTRIBUTES);
        sa.lpSecurityDescriptor = NULL;
        sa.bInheritHandle = TRUE;
        if (!CreatePipe(&hReadPipe, &hWritePipe, &sa, 0))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            return hr;
        }
        if (!SetHandleInformation(hWritePipe, HANDLE_FLAG_INHERIT, 0))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            return hr;
        }
        CAtlFile writePipe(hWritePipe);

        CString commandLine;
        commandLine.Format(_T("\"%s\""), lpApplicationName);

        int nSize = commandLine.GetLength() + 1;
        LPTSTR lpszCommandLine = new TCHAR[nSize];
        _tcscpy_s(lpszCommandLine, nSize, commandLine);

        STARTUPINFO startupInfo;
        ZeroMemory(&startupInfo, sizeof(STARTUPINFO));
        startupInfo.cb = sizeof(STARTUPINFO);
        startupInfo.hStdInput = hReadPipe;
        startupInfo.dwFlags = STARTF_USESHOWWINDOW | STARTF_USESTDHANDLES;
        if (pici)
        {
            startupInfo.wShowWindow = static_cast<WORD>(pici->nShow);
        }
        else
        {
            startupInfo.wShowWindow = SW_SHOWNORMAL;
        }

        PROCESS_INFORMATION processInformation;

        // Start the resizer
        CreateProcess(
            NULL,
            lpszCommandLine,
            NULL,
            NULL,
            TRUE,
            0,
            NULL,
            NULL,
            &startupInfo,
            &processInformation);
        delete[] lpszCommandLine;
        if (!CloseHandle(processInformation.hProcess))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            return hr;
        }
        if (!CloseHandle(processInformation.hThread))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            return hr;
        }

        // psiItemArray is NULL if called from InvokeCommand. This part is used for the MSI installer. It is not NULL if it is called from Invoke (MSIX).
        if (!psiItemArray)
        {
            // Stream the input files
            HDropIterator i(m_spdo);
            for (i.First(); !i.IsDone(); i.Next())
            {
                CString fileName(i.CurrentItem());
                // File name can't contain '?'
                fileName.Append(_T("?"));

                writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
            }
        }
        else
        {
            //m_pdtobj will be NULL when invoked from the MSIX build as Initialize is never called (IShellExtInit functions aren't called in case of MSIX).
            DWORD fileCount = 0;
            // Gets the list of files currently selected using the IShellItemArray
            psiItemArray->GetCount(&fileCount);
            // Iterate over the list of files
            for (DWORD i = 0; i < fileCount; i++)
            {
                IShellItem* shellItem;
                psiItemArray->GetItemAt(i, &shellItem);
                LPWSTR itemName;
                // Retrieves the entire file system path of the file from its shell item
                shellItem->GetDisplayName(SIGDN_FILESYSPATH, &itemName);
                CString fileName(itemName);
                // File name can't contain '?'
                fileName.Append(_T("?"));
                // Write the file path into the input stream for image resizer
                writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
            }
        }

        writePipe.Close();
    }
    Trace::InvokedRet(hr);

    return hr;
}

HRESULT __stdcall CPowerRenameMenu::GetTitle(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszName)
{
    return SHStrDup(app_name.c_str(), ppszName);
}

HRESULT __stdcall CPowerRenameMenu::GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon)
{
    if (!CSettingsInstance().GetShowIconOnMenu())
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

HRESULT __stdcall CPowerRenameMenu::GetState(IShellItemArray* /*psiItemArray*/, BOOL /*fOkToBeSlow*/, EXPCMDSTATE* pCmdState)
{
    *pCmdState = CSettingsInstance().GetEnabled() ? ECS_ENABLED : ECS_HIDDEN;
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
    HRESULT hr = E_OUTOFMEMORY;
    if (pInvokeData)
    {
        pInvokeData->hwndParent = nullptr;
        hr = CoMarshalInterThreadInterfaceInStream(__uuidof(psiItemArray), psiItemArray, &(pInvokeData->pstrm));
        if (!SUCCEEDED(hr))
        {
            return E_FAIL;
        }
        // Prevent Shutting down before PowerRenameUI is created
        ModuleAddRef();
        hr = RunPowerRename(nullptr, psiItemArray);
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
