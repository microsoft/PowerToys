#include "pch.h"

#include "ExplorerCommand.h"
#include "Constants.h"

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
    return SHStrDup(constants::localizable::CommandTitle, ppszName);
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
    // This should depend on the settings
    // For now we'll just keep it always enabled.
    *pCmdState = ECS_ENABLED;
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::Invoke(IShellItemArray* psiItemArray, IBindCtx* pbc)
{
    // This should call the main exe.
    // For now we'll just show a message box.
    MessageBoxW(NULL, L"OK", L"OK", MB_OK);
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
    HRESULT hr = E_UNEXPECTED;
    if (m_data_obj && !(uFlags & (CMF_DEFAULTONLY | CMF_VERBSONLY | CMF_OPTIMIZEFORINVOKE)))
    {
        MENUITEMINFO mii;
        mii.cbSize = sizeof(MENUITEMINFO);
        mii.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_STATE;
        mii.wID = idCmdFirst++;
        mii.fType = MFT_STRING;

        hr = SHStrDupW(constants::localizable::CommandTitle, &mii.dwTypeData);
        if (FAILED(hr))
        {
            return hr;
        }

        mii.fState = MFS_ENABLED;

        // TODO icon from file

        if (!InsertMenuItem(hmenu, indexMenu, TRUE, &mii))
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

IFACEMETHODIMP ExplorerCommand::InvokeCommand(CMINVOKECOMMANDINFO* pici)
{
    // TODO implement proper invocation and test
    // This should call the main exe.
    // For now we'll just show a message box.
    MessageBoxW(NULL, L"InvokeCommand", L"InvokeCommand", MB_OK);
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

ExplorerCommand::~ExplorerCommand()
{
    if (m_data_obj)
    {
        m_data_obj->Release();
    }
}
