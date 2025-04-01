// ContextMenuHandler.cpp : Implementation of CContextMenuHandler

#include "pch.h"
#include "ContextMenuHandler.h"

#include <Settings.h>
#include <trace.h>

#include <common/themes/icon_helpers.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/HDropIterator.h>
#include <common/utils/package.h>

extern HINSTANCE g_hInst_imageResizer;

CContextMenuHandler::CContextMenuHandler()
{
    m_pidlFolder = NULL;
    m_pdtobj = NULL;
    context_menu_caption = GET_RESOURCE_STRING_FALLBACK(IDS_IMAGERESIZER_CONTEXT_MENU_ENTRY, L"Resize with Image Resizer");
    context_menu_caption_here = GET_RESOURCE_STRING_FALLBACK(IDS_IMAGERESIZER_CONTEXT_MENU_ENTRY_HERE, L"Resize with Image Resizer here");
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

HRESULT CContextMenuHandler::Initialize(_In_opt_ PCIDLIST_ABSOLUTE pidlFolder, _In_opt_ IDataObject* pdtobj, _In_opt_ HKEY /*hkeyProgID*/)
{
    Uninitialize();

    if (!CSettingsInstance().GetEnabled())
    {
        return E_FAIL;
    }

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
    if (uFlags & CMF_DEFAULTONLY)
        return S_OK;

    if (!CSettingsInstance().GetEnabled())
        return E_FAIL;

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
    AssocGetPerceivedType(pszExt, &type, &flag, NULL);

    free(pszPath);
    bool dragDropFlag = false;
    // If selected file is an image...
    if (type == PERCEIVED_TYPE_IMAGE)
    {
        HRESULT hr = E_UNEXPECTED;
        wchar_t strResizePictures[128] = { 0 };
        // If handling drag-and-drop...
        if (m_pidlFolder)
        {
            dragDropFlag=true;
            wcscpy_s(strResizePictures, ARRAYSIZE(strResizePictures), context_menu_caption_here.c_str());
        }
        else
        {
            wcscpy_s(strResizePictures, ARRAYSIZE(strResizePictures), context_menu_caption.c_str());
        }

        MENUITEMINFO mii;
        mii.cbSize = sizeof(MENUITEMINFO);
        mii.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_STATE;
        mii.wID = idCmdFirst + ID_RESIZE_PICTURES;
        mii.fType = MFT_STRING;
        mii.dwTypeData = (PWSTR)strResizePictures;
        mii.fState = MFS_ENABLED;
        HICON hIcon = static_cast<HICON>(LoadImage(g_hInst_imageResizer, MAKEINTRESOURCE(IDI_RESIZE_PICTURES), IMAGE_ICON, 16, 16, 0));
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
            m_etwTrace.UpdateState(true);

            hr = HRESULT_FROM_WIN32(GetLastError());
            Trace::QueryContextMenuError(hr);

            m_etwTrace.Flush();
            m_etwTrace.UpdateState(false);
        }
        else
        {
            hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        }
        return hr;
    }

    return S_OK;
}

HRESULT CContextMenuHandler::GetCommandString(UINT_PTR idCmd, UINT uType, _In_ UINT* /*pReserved*/, LPSTR pszName, UINT cchMax)
{
    if (idCmd == ID_RESIZE_PICTURES)
    {
        if (uType == GCS_VERBW)
        {
            wcscpy_s(reinterpret_cast<LPWSTR>(pszName), cchMax, RESIZE_PICTURES_VERBW);
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
    m_etwTrace.UpdateState(true);

    BOOL fUnicode = FALSE;
    Trace::Invoked();
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
        if (wcscmp((reinterpret_cast<CMINVOKECOMMANDINFOEX*>(pici))->lpVerbW, RESIZE_PICTURES_VERBW) == 0)
        {
            hr = ResizePictures(pici, nullptr);
        }
    }
    else if (LOWORD(pici->lpVerb) == ID_RESIZE_PICTURES)
    {
        hr = ResizePictures(pici, nullptr);
    }
    Trace::InvokedRet(hr);

    m_etwTrace.Flush();
    m_etwTrace.UpdateState(false);

    return hr;
}

// This function is used for both MSI and MSIX. If pici is null and psiItemArray is not null then this is called by Invoke(MSIX). If pici is not null and psiItemArray is null then this is called by InvokeCommand(MSI).
HRESULT CContextMenuHandler::ResizePictures(CMINVOKECOMMANDINFO* pici, IShellItemArray* psiItemArray)
{
    // Set the application path based on the location of the dll
    std::wstring path = get_module_folderpath(g_hInst_imageResizer);
    path = path + L"\\PowerToys.ImageResizer.exe";
    LPTSTR lpApplicationName = &path[0];
    // Create an anonymous pipe to stream filenames
    SECURITY_ATTRIBUTES sa;
    HANDLE hReadPipe;
    HANDLE hWritePipe;
    sa.nLength = sizeof(SECURITY_ATTRIBUTES);
    sa.lpSecurityDescriptor = NULL;
    sa.bInheritHandle = TRUE;
    HRESULT hr = E_FAIL;
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
            // Write the file path into the input stream for image resizer
            writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
        }
    }

    writePipe.Close();
    hr = S_OK;
    return hr;
}

HRESULT __stdcall CContextMenuHandler::GetTitle(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszName)
{
    return SHStrDup(context_menu_caption.c_str(), ppszName);
}

HRESULT __stdcall CContextMenuHandler::GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon)
{
    // Since ImageResizer is registered as a COM SurrogateServer the current module filename would be dllhost.exe. To get the icon we need the path of PowerToys.ImageResizerExt.dll, which can be obtained by passing the HINSTANCE of the dll
    std::wstring iconResourcePath = get_module_filename(g_hInst_imageResizer);
    iconResourcePath += L",-";
    iconResourcePath += std::to_wstring(IDI_RESIZE_PICTURES);
    return SHStrDup(iconResourcePath.c_str(), ppszIcon);
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
    if (!CSettingsInstance().GetEnabled())
    {
        *pCmdState = ECS_HIDDEN;
        return S_OK;
    }
    // Hide if the file is not an image
    *pCmdState = ECS_HIDDEN;
    // Suppressing C26812 warning as the issue is in the shtypes.h library
#pragma warning(suppress : 26812)
    PERCEIVED type;
    PERCEIVEDFLAG flag;
    IShellItem* shellItem;

    //Check extension of first item in the list (the item which is right-clicked on)
    HRESULT getItemAtResult = psiItemArray->GetItemAt(0, &shellItem);
    if(!SUCCEEDED(getItemAtResult)) {
        // Avoid crashes in the following code.
        return E_FAIL;
    }

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
    AssocGetPerceivedType(pszExt, &type, &flag, NULL);

    CoTaskMemFree(pszPath);
    // If selected file is an image...
    if (type == PERCEIVED_TYPE_IMAGE)
    {
        *pCmdState = ECS_ENABLED;
    }
    return S_OK;
}

HRESULT __stdcall CContextMenuHandler::GetFlags(EXPCMDFLAGS* pFlags)
{
    *pFlags = ECF_DEFAULT;
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
    m_etwTrace.UpdateState(true);

    Trace::Invoked();
    HRESULT hr = ResizePictures(nullptr, psiItemArray);
    Trace::InvokedRet(hr);

    m_etwTrace.Flush();
    m_etwTrace.UpdateState(false);

    return hr;
}
