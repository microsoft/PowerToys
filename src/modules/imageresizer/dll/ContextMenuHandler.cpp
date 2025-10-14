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
    if (type == PERCEIVED_TYPE_IMAGE)
    {
        HRESULT hr = E_UNEXPECTED;
        wchar_t strResizePictures[128] = {};

        if (m_pidlFolder)
        {
            dragDropFlag = true;
            wcscpy_s(strResizePictures, ARRAYSIZE(strResizePictures), context_menu_caption_here.c_str());
        }
        else
        {
            wcscpy_s(strResizePictures, ARRAYSIZE(strResizePictures), context_menu_caption.c_str());
        }

        MENUITEMINFO mii{};
        mii.cbSize = sizeof(MENUITEMINFO);
        mii.fMask = MIIM_FTYPE | MIIM_ID | MIIM_STATE | MIIM_STRING;
        mii.fType = MFT_STRING;
        mii.wID = idCmdFirst + ID_RESIZE_PICTURES;
        mii.dwTypeData = strResizePictures;
        mii.cch = ARRAYSIZE(strResizePictures);
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
            indexMenu++;
        }
        else
        {
            MENUITEMINFO miiExisting{};
            miiExisting.cbSize = sizeof(MENUITEMINFO);
            miiExisting.fMask = MIIM_TYPE;
            if (GetMenuItemInfo(hmenu, indexMenu, TRUE, &miiExisting) && miiExisting.fType == MFT_SEPARATOR)
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
    path += L"\\PowerToys.ImageResizer.exe";
    LPTSTR lpApplicationName = path.data();
    HRESULT hr = E_FAIL;

    GUID pipeGuid{};
    if (FAILED(CoCreateGuid(&pipeGuid)))
    {
        return hr;
    }

    wchar_t guidBuffer[64] = {};
    StringFromGUID2(pipeGuid, guidBuffer, ARRAYSIZE(guidBuffer));

    std::wstring pipeName = L"\\\\.\\pipe\\PowerToysImageResizer_";
    std::wstring guidString(guidBuffer);
    if (guidString.length() > 2)
    {
        pipeName.append(guidString.substr(1, guidString.length() - 2));
    }
    else
    {
        pipeName.append(guidString);
    }

    HANDLE hNamedPipe = CreateNamedPipeW(
        pipeName.c_str(),
        PIPE_ACCESS_OUTBOUND,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
        1,
        0,
        0,
        0,
        nullptr);

    if (hNamedPipe == INVALID_HANDLE_VALUE)
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    CString commandLine;
    commandLine.Format(_T("\"%s\""), lpApplicationName);

    CString arguments;

    // Set the output directory
    if (m_pidlFolder)
    {
        TCHAR szFolder[MAX_PATH];
        if (SHGetPathFromIDList(m_pidlFolder, szFolder))
        {
            arguments.AppendFormat(_T("/d \"%s\""), szFolder);
        }
    }

    CString pipeArgument(pipeName.c_str());
    if (!arguments.IsEmpty())
    {
        arguments.Append(_T(" "));
    }
    arguments.Append(pipeArgument);

    if (!arguments.IsEmpty())
    {
        commandLine.Append(_T(" "));
        commandLine.Append(arguments);
    }

    STARTUPINFO startupInfo{};
    startupInfo.cb = sizeof(STARTUPINFO);
    startupInfo.dwFlags = STARTF_USESHOWWINDOW;
    startupInfo.wShowWindow = pici ? static_cast<WORD>(pici->nShow) : SW_SHOWNORMAL;

    PROCESS_INFORMATION processInformation{};

    bool launchSucceeded = false;

    // Try MSIX sparse package first
    std::wstring packageFamilyName;
#if !defined(CIBUILD)
    packageFamilyName = L"djwsxzxb4ksa8";
#else
    packageFamilyName = L"8wekyb3d8bbwe";
#endif
    std::wstring aumidTarget = L"shell:AppsFolder\\Microsoft.PowerToys.SparseApp_" + packageFamilyName + L"!PowerToys.ImageResizerUI";

    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = SEE_MASK_FLAG_NO_UI;
    sei.lpVerb = L"open";
    sei.lpFile = aumidTarget.c_str();
    sei.lpParameters = arguments.IsEmpty() ? nullptr : arguments.GetString();
    sei.nShow = pici ? static_cast<WORD>(pici->nShow) : SW_SHOWNORMAL;

    if (ShellExecuteExW(&sei) && reinterpret_cast<INT_PTR>(sei.hInstApp) > 32)
    {
        launchSucceeded = true;
    }
    else
    {
        LPWSTR mutableCommandLine = commandLine.GetBuffer();
        BOOL created = CreateProcess(
            nullptr,
            mutableCommandLine,
            nullptr,
            nullptr,
            FALSE,
            0,
            nullptr,
            nullptr,
            &startupInfo,
            &processInformation);
        commandLine.ReleaseBuffer();
        if (!created)
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            CloseHandle(hNamedPipe);
            return hr;
        }

        CloseHandle(processInformation.hProcess);
        CloseHandle(processInformation.hThread);
        launchSucceeded = true;
    }

    if (!launchSucceeded)
    {
        CloseHandle(hNamedPipe);
        return E_FAIL;
    }

    BOOL connected = ConnectNamedPipe(hNamedPipe, nullptr);
    if (!connected && GetLastError() != ERROR_PIPE_CONNECTED)
    {
        hr = HRESULT_FROM_WIN32(GetLastError());
        CloseHandle(hNamedPipe);
        return hr;
    }

    CAtlFile writePipe;
    writePipe.Attach(hNamedPipe);
    hNamedPipe = INVALID_HANDLE_VALUE;

    // psiItemArray is NULL if called from InvokeCommand. This part is used for the MSI installer. It is not NULL if it is called from Invoke (MSIX).
    if (!psiItemArray)
    {
        // Stream the input files
        HDropIterator i(m_pdtobj);
        for (i.First(); !i.IsDone(); i.Next())
        {
            CString fileName(i.CurrentItem());
            fileName.Append(_T("\r\n"));

            hr = writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
            if (FAILED(hr))
            {
                writePipe.Close();
                return hr;
            }
        }
    }
    else
    {
        // m_pdtobj will be NULL when invoked from the MSIX build as Initialize is never called (IShellExtInit functions aren't called in case of MSIX).
        DWORD fileCount = 0;
        psiItemArray->GetCount(&fileCount);
        for (DWORD i = 0; i < fileCount; i++)
        {
            IShellItem* shellItem;
            psiItemArray->GetItemAt(i, &shellItem);
            LPWSTR itemName;
            shellItem->GetDisplayName(SIGDN_FILESYSPATH, &itemName);
            CString fileName(itemName);
            fileName.Append(_T("\r\n"));

            hr = writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
            if (FAILED(hr))
            {
                writePipe.Close();
                CoTaskMemFree(itemName);
                shellItem->Release();
                return hr;
            }
            CoTaskMemFree(itemName);
            shellItem->Release();
        }
    }

    hr = writePipe.Flush();
    if (FAILED(hr))
    {
        writePipe.Close();
        return hr;
    }
    writePipe.Close();
    return S_OK;
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
