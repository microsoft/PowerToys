#pragma once
#include "stdafx.h"
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
    IFACEMETHODIMP get_path(_Outptr_ PWSTR* path);
    IFACEMETHODIMP get_shellItem(_Outptr_ IShellItem** ppsi);
    IFACEMETHODIMP get_originalName(_Outptr_ PWSTR* originalName);
    IFACEMETHODIMP put_newName(_In_opt_ PCWSTR newName);
    IFACEMETHODIMP get_newName(_Outptr_ PWSTR* newName);
    IFACEMETHODIMP get_isFolder(_Out_ bool* isFolder);
    IFACEMETHODIMP get_isSubFolderContent(_Out_ bool* isSubFolderContent);
    IFACEMETHODIMP get_selected(_Out_ bool* selected);
    IFACEMETHODIMP put_selected(_In_ bool selected);
    IFACEMETHODIMP get_id(_Out_ int* id);
    IFACEMETHODIMP get_iconIndex(_Out_ int* iconIndex);
    IFACEMETHODIMP get_depth(_Out_ UINT* depth);
    IFACEMETHODIMP put_depth(_In_ int depth);
    IFACEMETHODIMP Reset();
    IFACEMETHODIMP ShouldRenameItem(_In_ DWORD flags, _Out_ bool* shouldRename);

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

    bool     m_selected = true;
    bool     m_isFolder = false;
    int      m_id = -1;
    int      m_iconIndex = -1;
    UINT     m_depth = 0;
    HRESULT  m_error = S_OK;
    PWSTR    m_path = nullptr;
    PWSTR    m_originalName = nullptr;
    PWSTR    m_newName = nullptr;
    CSRWLock m_lock;
    long     m_refCount = 0;
};