#include "pch.h"

#include "ExplorerCommand.h"
#include "dllmain.h"
#include "Generated Files/resource.h"

#include "FileLocksmithLib/Constants.h"
#include "FileLocksmithLib/Settings.h"
#include "FileLocksmithLib/Trace.h"

#include <common/themes/icon_helpers.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>

// Implementations of inherited IUnknown methods

IFACEMETHODIMP ExplorerCommand::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(ExplorerCommand, IExplorerCommand),
        QITABENT(ExplorerCommand, IShellExtInit),
        QITABENT(ExplorerCommand, IContextMenu),
        { 0, 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG) ExplorerCommand::AddRef()
{
    return ++m_ref_count;
}

IFACEMETHODIMP_(ULONG) ExplorerCommand::Release()
{
    auto result = --m_ref_count;
    if (result == 0)
    {
        delete this;
    }
    return result;
}

// Implementations of inherited IExplorerCommand methods

IFACEMETHODIMP ExplorerCommand::GetTitle(IShellItemArray* psiItemArray, LPWSTR* ppszName)
{
    return SHStrDup(context_menu_caption.c_str(), ppszName);
}

IFACEMETHODIMP ExplorerCommand::GetIcon(IShellItemArray* psiItemArray, LPWSTR* ppszIcon)
{
    std::wstring iconResourcePath = get_module_filename();
    iconResourcePath += L",-";
    iconResourcePath += std::to_wstring(IDI_FILELOCKSMITH);
    return SHStrDup(iconResourcePath.c_str(), ppszIcon);
}

IFACEMETHODIMP ExplorerCommand::GetToolTip(IShellItemArray* psiItemArray, LPWSTR* ppszInfotip)
{
    // No tooltip for now
    return E_NOTIMPL;
}

IFACEMETHODIMP ExplorerCommand::GetCanonicalName(GUID* pguidCommandName)
{
    *pguidCommandName = __uuidof(this);
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetState(IShellItemArray* psiItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState)
{
    *pCmdState = FileLocksmithSettingsInstance().GetEnabled() ? ECS_ENABLED : ECS_HIDDEN;
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::Invoke(IShellItemArray* psiItemArray, IBindCtx* pbc)
{
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetFlags(EXPCMDFLAGS* pFlags)
{
    *pFlags = ECF_DEFAULT;
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    *ppEnum = NULL;
    return E_NOTIMPL;
}

// Implementations of inherited IShellExtInit methods

IFACEMETHODIMP ExplorerCommand::Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY hkeyProgID)
{
    m_data_obj = NULL;

    if (!FileLocksmithSettingsInstance().GetEnabled())
    {
        return E_FAIL;
    }

    if (pdtobj)
    {
        m_data_obj = pdtobj;
    }
    return S_OK;
}

// Implementations of inherited IContextMenu methods

IFACEMETHODIMP ExplorerCommand::QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags)
{
    // Skip if disabled
    if (!FileLocksmithSettingsInstance().GetEnabled())
    {
        return E_FAIL;
    }

    if (FileLocksmithSettingsInstance().GetShowInExtendedContextMenu() && !(uFlags & CMF_EXTENDEDVERBS))
    {
        return E_FAIL;
    }

    HRESULT hr = E_UNEXPECTED;
    if (m_data_obj && !(uFlags & (CMF_DEFAULTONLY | CMF_VERBSONLY | CMF_OPTIMIZEFORINVOKE)))
    {
        wchar_t menuName[128] = { 0 };
        wcscpy_s(menuName, ARRAYSIZE(menuName), context_menu_caption.c_str());

        MENUITEMINFO mii;
        mii.cbSize = sizeof(MENUITEMINFO);
        mii.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_STATE;
        mii.wID = idCmdFirst++;
        mii.fType = MFT_STRING;
        mii.dwTypeData = (PWSTR)menuName;
        mii.fState = MFS_ENABLED;

        // icon from file
        HICON hIcon = static_cast<HICON>(LoadImage(globals::instance, MAKEINTRESOURCE(IDI_FILELOCKSMITH), IMAGE_ICON, 16, 16, 0));
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

        if (!InsertMenuItem(hmenu, indexMenu, TRUE, &mii))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            Trace::QueryContextMenuError(hr);
        }
        else
        {
            hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        }
    }

    return hr;
}

IFACEMETHODIMP ExplorerCommand::InvokeCommand(CMINVOKECOMMANDINFO* pici)
{
    Trace::Invoked();
    ipc::Writer writer;

    if (HRESULT result = writer.start(); FAILED(result))
    {
        Trace::InvokedRet(result);
        return result;
    }

    if (HRESULT result = LaunchUI(pici, &writer); FAILED(result))
    {
        Trace::InvokedRet(result);
        return result;
    }

    IShellItemArray* shell_item_array;
    HRESULT result = SHCreateShellItemArrayFromDataObject(m_data_obj, __uuidof(IShellItemArray), reinterpret_cast<void**>(&shell_item_array));
    if (SUCCEEDED(result))
    {
        DWORD num_items;
        shell_item_array->GetCount(&num_items);
        for (DWORD i = 0; i < num_items; i++)
        {
            IShellItem* item;
            result = shell_item_array->GetItemAt(i, &item);
            if (SUCCEEDED(result))
            {
                LPWSTR file_path;
                result = item->GetDisplayName(SIGDN_FILESYSPATH, &file_path);
                if (SUCCEEDED(result))
                {
                    // TODO Aggregate items and send to UI
                    writer.add_path(file_path);
                    CoTaskMemFree(file_path);
                }

                item->Release();
            }
        }

        shell_item_array->Release();
    }

    Trace::InvokedRet(S_OK);
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetCommandString(UINT_PTR idCmd, UINT uType, UINT* pReserved, CHAR* pszName, UINT cchMax)
{
    return E_NOTIMPL;
}

HRESULT ExplorerCommand::s_CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject)
{
    *ppvObject = NULL;
    HRESULT hr = E_OUTOFMEMORY;
    ExplorerCommand* pNew = new (std::nothrow) ExplorerCommand;
    if (pNew)
    {
        hr = pNew->QueryInterface(riid, ppvObject);
        pNew->Release();
    }
    return hr;
}

ExplorerCommand::ExplorerCommand()
{
    ++globals::ref_count;
    context_menu_caption = GET_RESOURCE_STRING_FALLBACK(IDS_FILELOCKSMITH_CONTEXT_MENU_ENTRY, L"Unlock with File Locksmith");
}

ExplorerCommand::~ExplorerCommand()
{
    --globals::ref_count;
}

HRESULT ExplorerCommand::LaunchUI(CMINVOKECOMMANDINFO* pici, ipc::Writer* writer)
{
    // Compute exe path
    std::wstring exe_path = get_module_folderpath(globals::instance);
    exe_path += L'\\';
    exe_path += constants::nonlocalizable::FileNameUIExe;

    STARTUPINFO startupInfo;
    ZeroMemory(&startupInfo, sizeof(STARTUPINFO));
    startupInfo.cb = sizeof(STARTUPINFO);
    startupInfo.dwFlags = STARTF_USESHOWWINDOW;

    if (pici)
    {
        startupInfo.wShowWindow = pici->nShow;
    }
    else
    {
        startupInfo.wShowWindow = SW_SHOWNORMAL;
    }

    PROCESS_INFORMATION processInformation;
    std::wstring command_line = L"\"";
    command_line += exe_path;
    command_line += L"\"\0";

    CreateProcessW(
        NULL,
        command_line.data(),
        NULL,
        NULL,
        TRUE,
        0,
        NULL,
        NULL,
        &startupInfo,
        &processInformation);

    // Discard handles
    CloseHandle(processInformation.hProcess);
    CloseHandle(processInformation.hThread);

    return S_OK;
}