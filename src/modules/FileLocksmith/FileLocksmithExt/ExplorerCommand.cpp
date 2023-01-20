#include "pch.h"

#include "ExplorerCommand.h"
#include "Constants.h"
#include "Settings.h"
#include "dllmain.h"
#include "Trace.h"
#include "Generated Files/resource.h"

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
    WCHAR buffer[128];
    LoadStringW(globals::instance, IDS_FILELOCKSMITH_COMMANDTITLE, buffer, ARRAYSIZE(buffer));
    return SHStrDupW(buffer, ppszName);
}

IFACEMETHODIMP ExplorerCommand::GetIcon(IShellItemArray* psiItemArray, LPWSTR* ppszIcon)
{
    // Path to the icon should be computed relative to the path of this module
    ppszIcon = NULL;
    return E_NOTIMPL;
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
    if (globals::enabled)
    {
        *pCmdState = ECS_ENABLED;
    }
    else
    {
        *pCmdState = ECS_HIDDEN;
    }
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
    m_data_obj = pdtobj;
    m_data_obj->AddRef();
    return S_OK;
}

// Implementations of inherited IContextMenu methods

IFACEMETHODIMP ExplorerCommand::QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags)
{
    // Skip if disabled
    if (!FileLocksmithSettingsInstance().GetEnabled())
    {
        return S_OK;
    }

    HRESULT hr = E_UNEXPECTED;
    if (m_data_obj && !(uFlags & (CMF_DEFAULTONLY | CMF_VERBSONLY | CMF_OPTIMIZEFORINVOKE)))
    {
        MENUITEMINFO mii;
        mii.cbSize = sizeof(MENUITEMINFO);
        mii.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_STATE;
        mii.wID = idCmdFirst++;
        mii.fType = MFT_STRING;

        hr = GetTitle(NULL, &mii.dwTypeData);
        if (FAILED(hr))
        {
            return hr;
        }

        mii.fState = MFS_ENABLED;

        // TODO icon from file

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
}

ExplorerCommand::~ExplorerCommand()
{
    if (m_data_obj)
    {
        m_data_obj->Release();
    }
    --globals::ref_count;
}

// Implementation taken from src/common/utils
// TODO reference that function
inline std::wstring get_module_folderpath(HMODULE mod = nullptr, const bool removeFilename = true)
{
    wchar_t buffer[MAX_PATH + 1];
    DWORD actual_length = GetModuleFileNameW(mod, buffer, MAX_PATH + 1);
    if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
    {
        const DWORD long_path_length = 0xFFFF; // should be always enough
        std::wstring long_filename(long_path_length, L'\0');
        actual_length = GetModuleFileNameW(mod, long_filename.data(), long_path_length);
        PathRemoveFileSpecW(long_filename.data());
        long_filename.resize(std::wcslen(long_filename.data()));
        long_filename.shrink_to_fit();
        return long_filename;
    }

    if (removeFilename)
    {
        PathRemoveFileSpecW(buffer);
    }
    return { buffer, static_cast<UINT>(lstrlenW(buffer)) };
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
