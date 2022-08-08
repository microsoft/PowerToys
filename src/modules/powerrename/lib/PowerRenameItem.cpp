#include "pch.h"
#include "PowerRenameItem.h"

int CPowerRenameItem::s_id = 0;

IFACEMETHODIMP_(ULONG)
CPowerRenameItem::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG)
CPowerRenameItem::Release()
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

IFACEMETHODIMP CPowerRenameItem::PutPath(_In_opt_ PCWSTR newPath)
{
    CSRWSharedAutoLock lock(&m_lock);
    CoTaskMemFree(m_path);
    m_path = nullptr;
    HRESULT hr = S_OK;
    if (newPath != nullptr)
    {
        hr = SHStrDup(newPath, &m_path);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::GetPath(_Outptr_ PWSTR* path)
{
    *path = nullptr;
    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = E_FAIL;
    if (m_path)
    {
        hr = SHStrDup(m_path, path);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::GetTime(_Outptr_ SYSTEMTIME* time)
{
    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = E_FAIL;

    if (m_isTimeParsed)
    {
        hr = S_OK;
    }
    else
    {
        HANDLE hFile = CreateFileW(m_path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, NULL);
        if (hFile != INVALID_HANDLE_VALUE)
        {
            FILETIME CreationTime;
            if (GetFileTime(hFile, &CreationTime, NULL, NULL))
            {
                SYSTEMTIME SystemTime, LocalTime;
                if (FileTimeToSystemTime(&CreationTime, &SystemTime))
                {
                    if (SystemTimeToTzSpecificLocalTime(NULL, &SystemTime, &LocalTime))
                    {
                        m_time = LocalTime;
                        m_isTimeParsed = true;
                        hr = S_OK;
                    }
                }
            }
        }
        CloseHandle(hFile);
    }
    *time = m_time;
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::GetShellItem(_Outptr_ IShellItem** ppsi)
{
    return SHCreateItemFromParsingName(m_path, nullptr, IID_PPV_ARGS(ppsi));
}

IFACEMETHODIMP CPowerRenameItem::PutOriginalName(_In_opt_ PCWSTR originalName)
{
    CSRWSharedAutoLock lock(&m_lock);
    CoTaskMemFree(m_originalName);
    m_originalName = nullptr;
    HRESULT hr = S_OK;
    if (originalName != nullptr)
    {
        hr = SHStrDup(originalName, &m_originalName);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::GetOriginalName(_Outptr_ PWSTR* originalName)
{
    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = E_FAIL;
    if (m_originalName)
    {
        hr = SHStrDup(m_originalName, originalName);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::PutNewName(_In_opt_ PCWSTR newName)
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

IFACEMETHODIMP CPowerRenameItem::GetNewName(_Outptr_ PWSTR* newName)
{
    CSRWSharedAutoLock lock(&m_lock);
    HRESULT hr = S_OK;
    if (m_newName)
    {
        hr = SHStrDup(m_newName, newName);
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameItem::GetIsFolder(_Out_ bool* isFolder)
{
    CSRWSharedAutoLock lock(&m_lock);
    *isFolder = m_isFolder;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::GetIsSubFolderContent(_Out_ bool* isSubFolderContent)
{
    CSRWSharedAutoLock lock(&m_lock);
    *isSubFolderContent = m_depth > 0;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::GetSelected(_Out_ bool* selected)
{
    CSRWSharedAutoLock lock(&m_lock);
    *selected = m_selected;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::PutSelected(_In_ bool selected)
{
    CSRWSharedAutoLock lock(&m_lock);
    m_selected = selected;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::GetId(_Out_ int* id)
{
    CSRWSharedAutoLock lock(&m_lock);
    *id = m_id;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::GetDepth(_Out_ UINT* depth)
{
    *depth = m_depth;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::PutDepth(_In_ int depth)
{
    m_depth = depth;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::ShouldRenameItem(_In_ DWORD flags, _Out_ bool* shouldRename)
{
    // Should we perform a rename on this item given its
    // state and the options that were set?
    bool hasChanged = m_newName != nullptr && (lstrcmp(m_originalName, m_newName) != 0) && (lstrcmp(L"", m_newName) != 0);
    bool excludeBecauseFolder = (m_isFolder && (flags & PowerRenameFlags::ExcludeFolders));
    bool excludeBecauseFile = (!m_isFolder && (flags & PowerRenameFlags::ExcludeFiles));
    bool excludeBecauseSubFolderContent = (m_depth > 0 && (flags & PowerRenameFlags::ExcludeSubfolders));
    *shouldRename = (m_selected && m_canRename && hasChanged && !excludeBecauseFile &&
                     !excludeBecauseFolder && !excludeBecauseSubFolderContent);
    return S_OK;
}

IFACEMETHODIMP CPowerRenameItem::IsItemVisible(_In_ DWORD filter, _In_ DWORD flags, _Out_ bool* isItemVisible)
{
    switch (filter)
    {
    case PowerRenameFilters::None:
        *isItemVisible = true;
        break;
    case PowerRenameFilters::Selected:
        GetSelected(isItemVisible);
        break;
    case PowerRenameFilters::FlagsApplicable:
        *isItemVisible = !((m_isFolder && (flags & PowerRenameFlags::ExcludeFolders)) ||
                           (!m_isFolder && (flags & PowerRenameFlags::ExcludeFiles)) ||
                           (m_depth > 0 && (flags & PowerRenameFlags::ExcludeSubfolders)));
        break;
    case PowerRenameFilters::ShouldRename:
        ShouldRenameItem(flags, isItemVisible);
        break;
    }
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

    CPowerRenameItem* newRenameItem = new CPowerRenameItem();
    HRESULT hr = E_OUTOFMEMORY;
    if (newRenameItem)
    {
        hr = S_OK;
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
        hr = SHStrDup(PathFindFileName(m_path), &m_originalName);
        if (SUCCEEDED(hr))
        {
            // Check if we are a folder now so we can check this attribute quickly later
            // Also check if the shell allows us to rename the item.
            SFGAOF att = 0;
            hr = psi->GetAttributes(SFGAO_STREAM | SFGAO_FOLDER | SFGAO_CANRENAME, &att);
            if (SUCCEEDED(hr))
            {
                // Some items can be both folders and streams (ex: zip folders).
                m_isFolder = (att & SFGAO_FOLDER) && !(att & SFGAO_STREAM);
                // The shell lets us know if an item should not be renamed
                // (ex: user profile director, windows dir, etc).
                m_canRename = (att & SFGAO_CANRENAME);
            }
        }
    }

    return hr;
}
