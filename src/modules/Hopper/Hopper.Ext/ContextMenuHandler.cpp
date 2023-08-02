// ContextMenuHandler.cpp : Implementation of CContextMenuHandler

#include "pch.h"
#include "ContextMenuHandler.h"

#include <common/themes/icon_helpers.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/HDropIterator.h>
#include <common/utils/package.h>

extern HINSTANCE g_hInst_Hopper;

CContextMenuHandler::CContextMenuHandler()
{
    m_pidlFolder = nullptr;
    m_pdtobj = nullptr;
    app_name = GET_RESOURCE_STRING(IDS_HOPPER);
}

CContextMenuHandler::~CContextMenuHandler()
{
    Uninitialize();
}

void CContextMenuHandler::Uninitialize()
{
    CoTaskMemFree((LPVOID)m_pidlFolder);
    m_pidlFolder = nullptr;

    if (m_pdtobj)
    {
        m_pdtobj->Release();
        m_pdtobj = nullptr;
    }
}

HRESULT CContextMenuHandler::Initialize(_In_opt_ PCIDLIST_ABSOLUTE pidlFolder, _In_opt_ IDataObject* pdtobj, _In_opt_ HKEY /*hkeyProgID*/)
{
    Uninitialize();

    if (pidlFolder)
    {
        m_pidlFolder = ILClone(pidlFolder);
    }

    if (pdtobj)
    {
        m_pdtobj = pdtobj;
        m_pdtobj->AddRef();
    }

    return S_OK;
}

HRESULT CContextMenuHandler::QueryContextMenu(_In_ HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT /*idCmdLast*/, UINT uFlags)
{
    //idCmdFirst = idCmdLast;
    if (uFlags & CMF_DEFAULTONLY)
        return S_OK;

    // NB: We just check the first item. We could iterate through more if the first one doesn't meet the criteria
    HDropIterator i(m_pdtobj);
    i.First();
    if (i.IsDone())
    {
        return S_OK;
    }

// Suppressing C26812 warning as the issue is in the shtypes.h library
#pragma warning(suppress : 26812)
    PERCEIVED type;
    PERCEIVEDFLAG flag;
    LPTSTR pszPath = i.CurrentItem();
    if (nullptr == pszPath)
    {
        // Avoid crashes in the following code.
        return E_FAIL;
    }

    LPTSTR pszExt = PathFindExtension(pszPath);
    if (nullptr == pszExt)
    {
        free(pszPath);
        // Avoid crashes in the following code.
        return E_FAIL;
    }

    // TODO: Instead, detect whether there's a WIC codec installed that can handle this file
    AssocGetPerceivedType(pszExt, &type, &flag, nullptr);

    free(pszPath);

    HMENU hSubMenu = CreatePopupMenu();
    HMENU hFetchSubMenu = CreatePopupMenu();
    HMENU hAddSubMenu = CreatePopupMenu();

    wchar_t strAddToHopper[64] = { 0 };
    wchar_t strFetchHopper[64] = { 0 };
    wchar_t strMoveHere[64] = { 0 };
    wchar_t strCopyHere[64] = { 0 };
    wchar_t strViewHopper[64] = { 0 };
    wchar_t strAddHopper[64] = { 0 };
    wchar_t strAppendToRootHopper[64] = { 0 };
    wchar_t strAppendWithFoldersHopper[64] = { 0 };
    wchar_t strReplaceToRootHopper[64] = { 0 };
    wchar_t strReplaceWithFoldersHopper[64] = { 0 };
    wchar_t strClearHopper[64] = { 0 };

    // Suppressing C6031 warning since return value is not required.
#pragma warning(suppress : 6031)
    // Load 'PowerToys Hopper' string
    LoadString(g_hInst_Hopper, IDS_HOPPER, strAddToHopper, ARRAYSIZE(strAddToHopper));
    LoadString(g_hInst_Hopper, IDS_HOPPER_FETCH, strFetchHopper, ARRAYSIZE(strFetchHopper));
    LoadString(g_hInst_Hopper, IDS_HOPPER_MOVE_HERE, strMoveHere, ARRAYSIZE(strMoveHere));
    LoadString(g_hInst_Hopper, IDS_HOPPER_COPY_HERE, strCopyHere, ARRAYSIZE(strCopyHere));
    LoadString(g_hInst_Hopper, IDS_HOPPER_VIEW_HOPPER, strViewHopper, ARRAYSIZE(strViewHopper));
    LoadString(g_hInst_Hopper, IDS_HOPPER_ADD, strAddHopper, ARRAYSIZE(strAddHopper));
    LoadString(g_hInst_Hopper, IDS_HOPPER_APPEND_ROOT, strAppendToRootHopper, ARRAYSIZE(strAppendToRootHopper));
    LoadString(g_hInst_Hopper, IDS_HOPPER_APPEND_FOLDERS, strAppendWithFoldersHopper, ARRAYSIZE(strAppendWithFoldersHopper));
    LoadString(g_hInst_Hopper, IDS_HOPPER_REPLACE_ROOT, strReplaceToRootHopper, ARRAYSIZE(strReplaceToRootHopper));
    LoadString(g_hInst_Hopper, IDS_HOPPER_REPLACE_FOLDERS, strReplaceWithFoldersHopper, ARRAYSIZE(strReplaceWithFoldersHopper));
    LoadString(g_hInst_Hopper, IDS_HOPPER_CLEAR, strClearHopper, ARRAYSIZE(strClearHopper));

    MENUITEMINFO mii;
    mii.cbSize = sizeof(MENUITEMINFO);
    //mii.fMask = MIIM_STRING | MIIM_ID;
    //mii.wID = idCmdFirst++;
    //mii.fType = MFT_STRING;
    //mii.dwTypeData = (PWSTR)strAddToHopper;
    //mii.fState = MFS_ENABLED;

    /*HICON hIcon = static_cast<HICON>(LoadImage(g_hInst_Hopper, MAKEINTRESOURCE(IDI_RESIZE_PICTURES), IMAGE_ICON, 16, 16, 0));
    if (hIcon)
    {
        mii.fMask |= MIIM_BITMAP;
        if (m_hbmpIcon == nullptr)
        {
            m_hbmpIcon = CreateBitmapFromIcon(hIcon);
        }
        mii.hbmpItem = m_hbmpIcon;
        DestroyIcon(hIcon);
    }*/
    

    // indexMenu gets the first possible menu item index based on the location of the shellex registry key.
    // If the registry entry is under SystemFileAssociations for the image formats, ShellImagePreview (in Windows by default) will be at indexMenu=0
    // Shell ImagePreview consists of 4 menu items, a separator, Rotate right, Rotate left, and another separator
    // Check if the entry at indexMenu is a separator, insert the new menu item at indexMenu+1 if true
    /*MENUITEMINFO miiExisting;
    miiExisting.dwTypeData = nullptr;
    miiExisting.fMask = MIIM_TYPE;
    miiExisting.cbSize = sizeof(MENUITEMINFO);
    GetMenuItemInfo(hmenu, indexMenu, TRUE, &miiExisting);
    if (miiExisting.fType == MFT_SEPARATOR)
    {
        indexMenu++;
    }*/

    

    // sub menu
    int currentMenuPos = 0;

    mii.fMask = MIIM_SUBMENU | MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.hSubMenu = hSubMenu;
    mii.dwTypeData = strAddToHopper;
    if (!InsertMenuItem(hmenu, indexMenu, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    mii.fMask = MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.dwTypeData = strViewHopper;
    if (!InsertMenuItem(hSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    mii.wID = idCmdFirst++;
    mii.dwTypeData = strClearHopper;
    if (!InsertMenuItem(hSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    mii.fMask = MIIM_FTYPE | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.fType = MFT_SEPARATOR;
    if (!InsertMenuItem(hSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    mii.fMask = MIIM_SUBMENU | MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.hSubMenu = hFetchSubMenu;
    mii.dwTypeData = strFetchHopper;
    if (!InsertMenuItem(hSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    mii.hSubMenu = hAddSubMenu;
    mii.dwTypeData = strAddHopper;
    mii.wID = idCmdFirst++;
    if (!InsertMenuItem(hSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return  HRESULT_FROM_WIN32(GetLastError());
    }
    
    // "Fetch" menu
    currentMenuPos = 0;

    mii.fMask = MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.dwTypeData = strMoveHere;
    if (!InsertMenuItem(hFetchSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return  HRESULT_FROM_WIN32(GetLastError());
    }

    mii.fMask = MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.dwTypeData = strCopyHere;
    if (!InsertMenuItem(hFetchSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    // "Add" menu
    currentMenuPos = 0;
    mii.fMask = MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.dwTypeData = strAppendToRootHopper;
    if (!InsertMenuItem(hAddSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    mii.fMask = MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.dwTypeData = strAppendWithFoldersHopper;
    if (!InsertMenuItem(hAddSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    mii.fMask = MIIM_FTYPE | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.fType = MFT_SEPARATOR;
    if (!InsertMenuItem(hAddSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }
        
    mii.fMask = MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.dwTypeData = strReplaceToRootHopper;
    if (!InsertMenuItem(hAddSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    mii.fMask = MIIM_STRING | MIIM_ID;
    mii.wID = idCmdFirst++;
    mii.dwTypeData = strReplaceWithFoldersHopper;
    if (!InsertMenuItem(hAddSubMenu, currentMenuPos++, TRUE, &mii))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
}

HRESULT CContextMenuHandler::GetCommandString(UINT_PTR idCmd, UINT uType, _In_ UINT* /*pReserved*/, LPSTR pszName, UINT cchMax)
{

    if (uType == GCS_VERBW)
    {
        switch (idCmd) {
        case 0:
                wcscpy_s(reinterpret_cast<LPWSTR>(pszName), cchMax, HOPPER_VERBW);
                break;
        case 1:
                wcscpy_s(reinterpret_cast<LPWSTR>(pszName), cchMax, L"PowerToysHopperMoveHere");
                break;
        default:
                return E_INVALIDARG;
        }
        
    }
    
    return S_OK;
}

HRESULT CContextMenuHandler::InvokeCommand(_In_ CMINVOKECOMMANDINFO* pici)
{
    BOOL fUnicode = FALSE;
    HRESULT hr = E_FAIL;
    if (pici->cbSize == sizeof(CMINVOKECOMMANDINFOEX) && pici->fMask & CMIC_MASK_UNICODE)
    {
        fUnicode = TRUE;
    }

    if (!fUnicode && HIWORD(pici->lpVerb))
    {
    }
    else if (fUnicode && HIWORD(((CMINVOKECOMMANDINFOEX*)pici)->lpVerbW))
    {
        if (wcscmp((reinterpret_cast<CMINVOKECOMMANDINFOEX*>(pici))->lpVerbW, HOPPER_VERBW) == 0)
        {
            hr = SelectedFiles(pici, nullptr);
        }
        else if (wcscmp((reinterpret_cast<CMINVOKECOMMANDINFOEX*>(pici))->lpVerbW, L"PowerToysHopperMoveHere") == 0)
        {
            MessageBox(NULL, L"Hello, World!", L"My Message Box", MB_OK);
        }
    }
    else if (LOWORD(pici->lpVerb) == ID_HOPPER)
    {
        hr = SelectedFiles(pici, nullptr);
    }
    return hr;
}

// This function is used for both MSI and MSIX. If pici is null and psiItemArray is not null then this is called by Invoke(MSIX). If pici is not null and psiItemArray is null then this is called by InvokeCommand(MSI).
HRESULT CContextMenuHandler::SelectedFiles(CMINVOKECOMMANDINFO* pici, IShellItemArray* psiItemArray)
{
    // Set the application path based on the location of the dll
    std::wstring path = get_module_folderpath(g_hInst_Hopper);
    path = path + L"\\Hopper.exe";
    LPTSTR lpApplicationName = &path[0];
    // Create an anonymous pipe to stream filenames
    SECURITY_ATTRIBUTES sa;
    HANDLE hReadPipe;
    HANDLE hWritePipe;
    sa.nLength = sizeof(SECURITY_ATTRIBUTES);
    sa.lpSecurityDescriptor = nullptr;
    sa.bInheritHandle = TRUE;
    HRESULT hr;
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

    // Set the output directory
    if (m_pidlFolder)
    {
        TCHAR szFolder[MAX_PATH];
        SHGetPathFromIDList(m_pidlFolder, szFolder);

        commandLine.AppendFormat(_T(" /d \"%s\""), szFolder);
    }

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
        nullptr,
        lpszCommandLine,
        nullptr,
        nullptr,
        TRUE,
        0,
        nullptr,
        nullptr,
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
        HDropIterator i(m_pdtobj);
        for (i.First(); !i.IsDone(); i.Next())
        {
            CString fileName(i.CurrentItem());
            fileName.Append(_T("\r\n"));

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
            fileName.Append(_T("\r\n"));
            // Write the file path into the input stream for hopper
            writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
        }
    }

    writePipe.Close();
    hr = S_OK;
    return hr;
}

HRESULT __stdcall CContextMenuHandler::GetTitle(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszName)
{
    return SHStrDup(app_name.c_str(), ppszName);
}

HRESULT __stdcall CContextMenuHandler::GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon)
{
    // Suppress C4100 warnings
    (void)ppszIcon;

    return E_NOTIMPL;
    // Since Hopper is registered as a COM SurrogateServer the current module filename would be dllhost.exe. To get the icon we need the path of PowerToys.HopperExt.dll, which can be obtained by passing the HINSTANCE of the dll
    /* std::wstring iconResourcePath = get_module_filename(g_hInst_Hopper);
    iconResourcePath += L",-";
    iconResourcePath += std::to_wstring(IDI_RESIZE_PICTURES);
    return SHStrDup(iconResourcePath.c_str(), ppszIcon);*/
}

HRESULT __stdcall CContextMenuHandler::GetToolTip(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszInfotip)
{
    *ppszInfotip = nullptr;
    return E_NOTIMPL;
}

HRESULT __stdcall CContextMenuHandler::GetCanonicalName(GUID* pguidCommandName)
{
    *pguidCommandName = __uuidof(this);
    return S_OK;
}

HRESULT __stdcall CContextMenuHandler::GetState(IShellItemArray* psiItemArray, BOOL /*fOkToBeSlow*/, EXPCMDSTATE* pCmdState)
{
    // Suppressing C26812 warning as the issue is in the shtypes.h library
#pragma warning(suppress : 26812)
    PERCEIVED type;
    PERCEIVEDFLAG flag;
    IShellItem* shellItem;
    //Check extension of first item in the list (the item which is right-clicked on)
    psiItemArray->GetItemAt(0, &shellItem);
    LPTSTR pszPath;
    // Retrieves the entire file system path of the file from its shell item
    HRESULT getDisplayResult = shellItem->GetDisplayName(SIGDN_FILESYSPATH, &pszPath);
    if (S_OK != getDisplayResult || nullptr == pszPath)
    {
        // Avoid crashes in the following code.
        return E_FAIL;
    }

    LPTSTR pszExt = PathFindExtension(pszPath);
    if (nullptr == pszExt)
    {
        CoTaskMemFree(pszPath);
        // Avoid crashes in the following code.
        return E_FAIL;
    }

    // TODO: Instead, detect whether there's a WIC codec installed that can handle this file
    AssocGetPerceivedType(pszExt, &type, &flag, nullptr);

    CoTaskMemFree(pszPath);

    *pCmdState = ECS_ENABLED;

    return S_OK;
}

HRESULT __stdcall CContextMenuHandler::GetFlags(EXPCMDFLAGS* pFlags)
{
    *pFlags = ECF_HASSUBCOMMANDS;
    return S_OK;
}

HRESULT __stdcall CContextMenuHandler::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{ 
    *ppEnum = nullptr;
    return E_NOTIMPL;
}

// psiItemArray contains the list of files that have been selected when the context menu entry is invoked
HRESULT __stdcall CContextMenuHandler::Invoke(IShellItemArray* psiItemArray, IBindCtx* /*pbc*/)
{
    return SelectedFiles(nullptr, psiItemArray);
}
