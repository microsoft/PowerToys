// ContextMenuHandler.cpp : Implementation of CContextMenuHandler

#include "stdafx.h"
#include "ContextMenuHandler.h"
#include "HDropIterator.h"
#include "Settings.h"

extern HINSTANCE g_hInst_imageResizer;

CContextMenuHandler::CContextMenuHandler()
{
    m_pidlFolder = NULL;
    m_pdtobj = NULL;
}

CContextMenuHandler::~CContextMenuHandler()
{
    Uninitialize();
}

void CContextMenuHandler::Uninitialize()
{
    CoTaskMemFree((LPVOID)m_pidlFolder);
    m_pidlFolder = NULL;

    if (m_pdtobj)
    {
        m_pdtobj->Release();
        m_pdtobj = NULL;
    }
}

HRESULT CContextMenuHandler::Initialize(_In_opt_ PCIDLIST_ABSOLUTE pidlFolder, _In_opt_ IDataObject* pdtobj, _In_opt_ HKEY hkeyProgID)
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

HRESULT CContextMenuHandler::QueryContextMenu(_In_ HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags)
{
    if (uFlags & CMF_DEFAULTONLY)
    {
        return S_OK;
    }
    if (!CSettings::GetEnabled())
        return E_FAIL;
    // NB: We just check the first item. We could iterate through more if the first one doesn't meet the criteria
    HDropIterator i(m_pdtobj);
    i.First();
// Suppressing C26812 warning as the issue is in the shtypes.h library
#pragma warning(suppress : 26812)
    PERCEIVED type;
    PERCEIVEDFLAG flag;
    LPTSTR pszPath = i.CurrentItem();
    LPTSTR pszExt = PathFindExtension(pszPath);

    // TODO: Instead, detect whether there's a WIC codec installd that can handle this file (issue #7)
    AssocGetPerceivedType(pszExt, &type, &flag, NULL);

    free(pszPath);
    bool dragDropFlag = false;
    // If selected file is an image...
    if (type == PERCEIVED_TYPE_IMAGE)
    {
        HRESULT hr = E_UNEXPECTED;
        wchar_t strResizePictures[64] = { 0 };
        // If handling drag-and-drop...
        if (m_pidlFolder)
        {
            // Suppressing C6031 warning since return value is not required.
#pragma warning(suppress : 6031)
            // Load 'Resize pictures here' string
            LoadString(g_hInst_imageResizer, IDS_RESIZE_PICTURES_HERE, strResizePictures, ARRAYSIZE(strResizePictures));
            dragDropFlag = true;
        }
        else
        {
            // Suppressing C6031 warning since return value is not required.
#pragma warning(suppress : 6031)
            // Load 'Resize pictures' string
            LoadString(g_hInst_imageResizer, IDS_RESIZE_PICTURES, strResizePictures, ARRAYSIZE(strResizePictures));
        }

        MENUITEMINFO mii;
        mii.cbSize = sizeof(MENUITEMINFO);
        mii.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_STATE;
        mii.wID = idCmdFirst + ID_RESIZE_PICTURES;
        mii.fType = MFT_STRING;
        mii.dwTypeData = (PWSTR)strResizePictures;
        mii.fState = MFS_ENABLED;
        HICON hIcon = (HICON)LoadImage(g_hInst_imageResizer, MAKEINTRESOURCE(IDI_RESIZE_PICTURES), IMAGE_ICON, 16, 16, 0);
        if (hIcon)
        {
            mii.fMask |= MIIM_BITMAP;
            if (m_hbmpIcon == NULL)
            {
                m_hbmpIcon = CreateBitmapFromIcon(hIcon, 0, 0);
            }
            mii.hbmpItem = m_hbmpIcon;
            DestroyIcon(hIcon);
        }

        if (dragDropFlag)
        {
            // Insert the menu entry at indexMenu+1 since the first entry should be "Copy here"
            indexMenu++;
        }
        else
        {
            // indexMenu gets the first possible menu item index based on the location of the shellex registry key.
            // If the registry entry is under SystemFileAssociations for the image formats, ShellImagePreview (in Windows by default) will be at indexMenu=0
            // Shell ImagePreview consists of 4 menu items, a separator, Rotate right, Rotate left, and another separator
            // Check if the entry at indexMenu is a separator, insert the new menu item at indexMenu+1 if true
            MENUITEMINFO miiExisting;
            miiExisting.dwTypeData = NULL;
            miiExisting.fMask = MIIM_TYPE;
            miiExisting.cbSize = sizeof(MENUITEMINFO);
            GetMenuItemInfo(hmenu, indexMenu, TRUE, &miiExisting);
            if (miiExisting.fType == MFT_SEPARATOR)
            {
                indexMenu++;
            }
        }

        if (!InsertMenuItem(hmenu, indexMenu, TRUE, &mii))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
        }
        else
        {
            hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        }
        return hr;
    }

    return S_OK;
}

HRESULT CContextMenuHandler::GetCommandString(UINT_PTR idCmd, UINT uType, _In_ UINT* pReserved, LPSTR pszName, UINT cchMax)
{
    if (idCmd == ID_RESIZE_PICTURES)
    {
        if (uType == GCS_VERBW)
        {
            wcscpy_s((LPWSTR)pszName, cchMax, RESIZE_PICTURES_VERBW);
        }
    }
    else
    {
        return E_INVALIDARG;
    }

    return S_OK;
}

HRESULT CContextMenuHandler::InvokeCommand(_In_ CMINVOKECOMMANDINFO* pici)
{
    BOOL fUnicode = FALSE;

    if (pici->cbSize == sizeof(CMINVOKECOMMANDINFOEX) && pici->fMask & CMIC_MASK_UNICODE)
    {
        fUnicode = TRUE;
    }

    if (!fUnicode && HIWORD(pici->lpVerb))
    {
    }
    else if (fUnicode && HIWORD(((CMINVOKECOMMANDINFOEX*)pici)->lpVerbW))
    {
        if (wcscmp(((CMINVOKECOMMANDINFOEX*)pici)->lpVerbW, RESIZE_PICTURES_VERBW) == 0)
        {
            return ResizePictures(pici);
        }
    }
    else if (LOWORD(pici->lpVerb) == ID_RESIZE_PICTURES)
    {
        return ResizePictures(pici);
    }

    return E_FAIL;
}

// TODO: Error handling and memory management
HRESULT CContextMenuHandler::ResizePictures(CMINVOKECOMMANDINFO* pici)
{
    // Set the application path from the registry
    LPTSTR lpApplicationName = new TCHAR[MAX_PATH];
    ULONG nChars = MAX_PATH;
    CRegKey regKey;
    // Open registry key saved by installer under HKLM
    regKey.Open(HKEY_LOCAL_MACHINE, _T("Software\\Microsoft\\ImageResizer"), KEY_READ | KEY_WOW64_64KEY);
    regKey.QueryStringValue(NULL, lpApplicationName, &nChars);
    regKey.Close();

    // Create an anonymous pipe to stream filenames
    SECURITY_ATTRIBUTES sa;
    HANDLE hReadPipe;
    HANDLE hWritePipe;
    sa.nLength = sizeof(SECURITY_ATTRIBUTES);
    sa.lpSecurityDescriptor = NULL;
    sa.bInheritHandle = TRUE;
    CreatePipe(&hReadPipe, &hWritePipe, &sa, 0);
    SetHandleInformation(hWritePipe, HANDLE_FLAG_INHERIT, 0);
    CAtlFile writePipe(hWritePipe);

    CString commandLine;
    commandLine.Format(_T("\"%s\""), lpApplicationName);
    delete[] lpApplicationName;

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
    startupInfo.wShowWindow = pici->nShow;

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
    CloseHandle(processInformation.hProcess);
    CloseHandle(processInformation.hThread);

    // Stream the input files
    HDropIterator i(m_pdtobj);
    for (i.First(); !i.IsDone(); i.Next())
    {
        CString fileName(i.CurrentItem());
        fileName.Append(_T("\r\n"));

        writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
    }

    writePipe.Close();

    return S_OK;
}
