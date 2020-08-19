#pragma once
#include "pch.h"
#include "PowerRenameInterfaces.h"
#include "srwlock.h"

class CPowerRenameItem :
    public IPowerRenameItem,
    public IPowerRenameItemFactory
{
public:
    // IUnknown
    IFACEMETHODIMP  QueryInterface(_In_ REFIID iid, _Outptr_ void** resultInterface);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IPowerRenameItem
    IFACEMETHODIMP getPath(_Outptr_ PWSTR* path);
    IFACEMETHODIMP getDate(_Outptr_ SYSTEMTIME* date);
    IFACEMETHODIMP getShellItem(_Outptr_ IShellItem** ppsi);
    IFACEMETHODIMP getOriginalName(_Outptr_ PWSTR* originalName);
    IFACEMETHODIMP putNewName(_In_opt_ PCWSTR newName);
    IFACEMETHODIMP getNewName(_Outptr_ PWSTR* newName);
    IFACEMETHODIMP getIsFolder(_Out_ bool* isFolder);
    IFACEMETHODIMP getIsSubFolderContent(_Out_ bool* isSubFolderContent);
    IFACEMETHODIMP getSelected(_Out_ bool* selected);
    IFACEMETHODIMP putSelected(_In_ bool selected);
    IFACEMETHODIMP getId(_Out_ int* id);
    IFACEMETHODIMP getIconIndex(_Out_ int* iconIndex);
    IFACEMETHODIMP getDepth(_Out_ UINT* depth);
    IFACEMETHODIMP putDepth(_In_ int depth);
    IFACEMETHODIMP Reset();
    IFACEMETHODIMP ShouldRenameItem(_In_ DWORD flags, _Out_ bool* shouldRename);
    IFACEMETHODIMP IsItemVisible(_In_ DWORD filter, _In_ DWORD flags, _Out_ bool* isItemVisible);

    // IPowerRenameItemFactory
    IFACEMETHODIMP Create(_In_ IShellItem* psi, _Outptr_ IPowerRenameItem** ppItem)
    {
        return CPowerRenameItem::s_CreateInstance(psi, IID_PPV_ARGS(ppItem));
    }

public:
    static HRESULT s_CreateInstance(_In_opt_ IShellItem* psi, _In_ REFIID iid, _Outptr_ void** resultInterface);

protected:
    static int s_id;
    CPowerRenameItem();
    virtual ~CPowerRenameItem();

    HRESULT _Init(_In_ IShellItem* psi);

    bool        m_selected = true;
    bool        m_isFolder = false;
    bool        m_isDateParsed = false;
    bool        m_canRename = true;
    int         m_id = -1;
    int         m_iconIndex = -1;
    UINT        m_depth = 0;
    HRESULT     m_error = S_OK;
    PWSTR       m_path = nullptr;
    PWSTR       m_originalName = nullptr;
    PWSTR       m_newName = nullptr;
    SYSTEMTIME  m_date;
    CSRWLock    m_lock;
    long        m_refCount = 0;
};
