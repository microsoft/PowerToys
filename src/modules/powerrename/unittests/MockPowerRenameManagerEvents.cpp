#include "stdafx.h"
#include "MockPowerRenameManagerEvents.h"

// IUnknown
IFACEMETHODIMP CMockPowerRenameManagerEvents::QueryInterface(__in REFIID riid, __deref_out void** ppv)
{
    static const QITAB qit[] =
    {
        QITABENT(CMockPowerRenameManagerEvents, IPowerRenameManagerEvents),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG) CMockPowerRenameManagerEvents::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG) CMockPowerRenameManagerEvents::Release()
{
    long refCount = InterlockedDecrement(&m_refCount);
    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

// IPowerRenameManagerEvents
IFACEMETHODIMP CMockPowerRenameManagerEvents::OnItemAdded(_In_ IPowerRenameItem* pItem)
{
    m_itemAdded = pItem;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnUpdate(_In_ IPowerRenameItem* pItem)
{
    m_itemUpdated = pItem;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnError(_In_ IPowerRenameItem* pItem)
{
    m_itemError = pItem;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRegExStarted(_In_ DWORD threadId)
{
    m_regExStarted = true;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRegExCanceled(_In_ DWORD threadId)
{
    m_regExCanceled = true;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRegExCompleted(_In_ DWORD threadId)
{
    m_regExCompleted = true;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRenameStarted()
{
    m_renameStarted = true;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameManagerEvents::OnRenameCompleted()
{
    m_renameCompleted = true;
    return S_OK;
}

HRESULT CMockPowerRenameManagerEvents::s_CreateInstance(_In_ IPowerRenameManager* psrm, _Outptr_ IPowerRenameUI** ppsrui)
{
    *ppsrui = nullptr;
    CMockPowerRenameManagerEvents* events = new CMockPowerRenameManagerEvents();
    HRESULT hr = events != nullptr ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
    {
        hr = events->QueryInterface(IID_PPV_ARGS(ppsrui));
        events->Release();
    }

    return hr;
}

