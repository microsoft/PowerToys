#include "pch.h"
#include "MockPowerRenameManagerEvents.h"

// IUnknown
IFACEMETHODIMP CMockPowerRenameManagerEvents::QueryInterface(__in REFIID riid, __deref_out void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(CMockPowerRenameManagerEvents, IPowerRenameManagerEvents),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG)
CMockPowerRenameManagerEvents::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG)
CMockPowerRenameManagerEvents::Release()
{
    long refCount = InterlockedDecrement(&m_refCount);
    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

// IPowerRenameManagerEvents
IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRename(_In_ IPowerRenameItem* pItem)
{
    m_itemRenamed = pItem;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnError(_In_ IPowerRenameItem* pItem)
{
    m_itemError = pItem;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRegExStarted(_In_ DWORD /*threadId*/)
{
    m_regExStarted = true;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRegExCanceled(_In_ DWORD /*threadId*/)
{
    m_regExCanceled = true;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRegExCompleted(_In_ DWORD /*threadId*/)
{
    m_regExCompleted = true;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRenameStarted()
{
    m_renameStarted = true;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRenameCompleted(bool closeUIWindowAfterRenaming)
{
    m_renameCompleted = true;
    m_closeUIWindowAfterRenaming = closeUIWindowAfterRenaming;
    return S_OK;
}
