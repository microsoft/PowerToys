#pragma once
#include <PowerRenameInterfaces.h>

class CMockPowerRenameManagerEvents :
    public IPowerRenameManagerEvents
{
public:
    CMockPowerRenameManagerEvents() :
        m_refCount(1)
    {
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(__in REFIID riid, __deref_out void** ppv);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();
    
    // IPowerRenameManagerEvents
    IFACEMETHODIMP OnItemAdded(_In_ IPowerRenameItem* renameItem);
    IFACEMETHODIMP OnUpdate(_In_ IPowerRenameItem* renameItem);
    IFACEMETHODIMP OnError(_In_ IPowerRenameItem* renameItem);
    IFACEMETHODIMP OnRegExStarted(_In_ DWORD threadId);
    IFACEMETHODIMP OnRegExCanceled(_In_ DWORD threadId);
    IFACEMETHODIMP OnRegExCompleted(_In_ DWORD threadId);
    IFACEMETHODIMP OnRenameStarted();
    IFACEMETHODIMP OnRenameCompleted();

    static HRESULT s_CreateInstance(_In_ IPowerRenameManager* psrm, _Outptr_ IPowerRenameUI** ppsrui);

    ~CMockPowerRenameManagerEvents()
    {
    }

    CComPtr<IPowerRenameItem> m_itemAdded;
    CComPtr<IPowerRenameItem> m_itemUpdated;
    CComPtr<IPowerRenameItem> m_itemError;
    bool m_regExStarted = false;
    bool m_regExCanceled = false;
    bool m_regExCompleted = false;
    bool m_renameStarted = false;
    bool m_renameCompleted = false;
    long m_refCount = 0;
};