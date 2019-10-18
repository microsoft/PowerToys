#include "stdafx.h"
#include "PowerRenameItem.h"
#include "helpers.h"

int CPowerRenameItem::s_id = 0;

IFACEMETHODIMP_(ULONG) CPowerRenameItem::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG) CPowerRenameItem::Release()
{
    long refCount = InterlockedDecrement(&m_refCount);

    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

IFACEMETHODIMP CPowerRenameItem::QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(CPowerRenameItem, IPowerRenameItem),
        QITABENT(CPowerRenameItem, IPowerRenameItemFactory),
        { 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP CPowerRenameItem::get_path(_Outptr_ PWSTR* path)
{
    *path = nullptr;
    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = m_path ? S_OK : E_FAIL;
    if (SUCCEEDED(hr))
    {
        hr = SHStrDup(m_path, path);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::get_shellItem(_Outptr_ IShellItem** ppsi)
{
    return SHCreateItemFromParsingName(m_path, nullptr, IID_PPV_ARGS(ppsi));
}

IFACEMETHODIMP CPowerRenameItem::get_originalName(_Outptr_ PWSTR* originalName)
{
    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = m_originalName ? S_OK : E_FAIL;
    if (SUCCEEDED(hr))
    {
        hr = SHStrDup(m_originalName, originalName);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::put_newName(_In_opt_ PCWSTR newName)
{
    CSRWSharedAutoLock lock(&m_lock);
    CoTaskMemFree(m_newName);
    m_newName = nullptr;
    HRESULT hr = S_OK;
    if (newName != nullptr)
    {
        hr = SHStrDup(newName, &m_newName);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::get_newName(_Outptr_ PWSTR* newName)
{
    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = m_newName ? S_OK : E_FAIL;
    if (SUCCEEDED(hr))
    {
        hr = SHStrDup(m_newName, newName);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::get_isFolder(_Out_ bool* isFolder)
{
    CSRWSharedAutoLock lock(&m_lock);
    *isFolder = m_isFolder;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::get_isSubFolderContent(_Out_ bool* isSubFolderContent)
{
    CSRWSharedAutoLock lock(&m_lock);
    *isSubFolderContent = m_depth > 0;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::get_selected(_Out_ bool* selected)
{
    CSRWSharedAutoLock lock(&m_lock);
    *selected = m_selected;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::put_selected(_In_ bool selected)
{
    CSRWSharedAutoLock lock(&m_lock);
    m_selected = selected;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::get_id(_Out_ int* id)
{
    CSRWSharedAutoLock lock(&m_lock);
    *id = m_id;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::get_iconIndex(_Out_ int* iconIndex)
{
    if (m_iconIndex == -1)
    {
        GetIconIndexFromPath((PCWSTR)m_path, &m_iconIndex);
    }
    *iconIndex = m_iconIndex;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::get_depth(_Out_ UINT* depth)
{
    *depth = m_depth;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::put_depth(_In_ int depth)
{
    m_depth = depth;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::ShouldRenameItem(_In_ DWORD flags, _Out_ bool* shouldRename)
{
    // Should we perform a rename on this item given its
    // state and the options that were set?
    bool hasChanged = m_newName != nullptr && (lstrcmp(m_originalName, m_newName) != 0);
    bool excludeBecauseFolder = (m_isFolder && (flags & PowerRenameFlags::ExcludeFolders));
    bool excludeBecauseFile = (!m_isFolder && (flags & PowerRenameFlags::ExcludeFiles));
    bool excludeBecauseSubFolderContent = (m_depth > 0 && (flags & PowerRenameFlags::ExcludeSubfolders));
    *shouldRename = (m_selected && hasChanged && !excludeBecauseFile &&
                     !excludeBecauseFolder && !excludeBecauseSubFolderContent);

    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::Reset()
{
    CSRWSharedAutoLock lock(&m_lock);
    CoTaskMemFree(m_newName);
    m_newName = nullptr;
    return S_OK;
}

HRESULT CPowerRenameItem::s_CreateInstance(_In_opt_ IShellItem* psi, _In_ REFIID iid, _Outptr_ void** resultInterface)
{
    *resultInterface = nullptr;

    CPowerRenameItem *newRenameItem = new CPowerRenameItem();
    HRESULT hr = newRenameItem ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
    {
        if (psi != nullptr)
        {
            hr = newRenameItem->_Init(psi);
        }

        if (SUCCEEDED(hr))
        {
            hr = newRenameItem->QueryInterface(iid, resultInterface);
        }

        newRenameItem->Release();
    }
    return hr;
}

CPowerRenameItem::CPowerRenameItem() :
    m_refCount(1),
    m_id(++s_id)
{
}

CPowerRenameItem::~CPowerRenameItem()
{
    CoTaskMemFree(m_path);
    CoTaskMemFree(m_newName);
    CoTaskMemFree(m_originalName);
}

HRESULT CPowerRenameItem::_Init(_In_ IShellItem* psi)
{
    // Get the full filesystem path from the shell item
    HRESULT hr = psi->GetDisplayName(SIGDN_FILESYSPATH, &m_path);
    if (SUCCEEDED(hr))
    {
        hr = psi->GetDisplayName(SIGDN_NORMALDISPLAY, &m_originalName);
        if (SUCCEEDED(hr))
        {
            // Check if we are a folder now so we can check this attribute quickly later
            SFGAOF att = 0;
            hr = psi->GetAttributes(SFGAO_STREAM | SFGAO_FOLDER, &att);
            if (SUCCEEDED(hr))
            {
                // Some items can be both folders and streams (ex: zip folders).
                m_isFolder = (att & SFGAO_FOLDER) && !(att & SFGAO_STREAM);
            }
        }
    }

    return hr;
}
