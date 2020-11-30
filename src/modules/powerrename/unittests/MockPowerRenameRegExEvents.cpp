#include "pch.h"
#include "MockPowerRenameRegExEvents.h"

IFACEMETHODIMP_(ULONG)
CMockPowerRenameRegExEvents::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG)
CMockPowerRenameRegExEvents::Release()
{
    long refCount = InterlockedDecrement(&m_refCount);

    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

IFACEMETHODIMP CMockPowerRenameRegExEvents::QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(CMockPowerRenameRegExEvents, IPowerRenameRegExEvents),
        { 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP CMockPowerRenameRegExEvents::OnSearchTermChanged(_In_ PCWSTR searchTerm)
{
    CoTaskMemFree(m_searchTerm);
    m_searchTerm = nullptr;
    if (searchTerm != nullptr)
    {
        SHStrDup(searchTerm, &m_searchTerm);
    }
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameRegExEvents::OnReplaceTermChanged(_In_ PCWSTR replaceTerm)
{
    CoTaskMemFree(m_replaceTerm);
    m_replaceTerm = nullptr;
    if (replaceTerm != nullptr)
    {
        SHStrDup(replaceTerm, &m_replaceTerm);
    }
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameRegExEvents::OnFlagsChanged(_In_ DWORD flags)
{
    m_flags = flags;
    return S_OK;
}

IFACEMETHODIMP CMockPowerRenameRegExEvents::OnFileTimeChanged(_In_ SYSTEMTIME fileTime)
{
    m_fileTime = fileTime;
    return S_OK;
}

HRESULT CMockPowerRenameRegExEvents::s_CreateInstance(_Outptr_ IPowerRenameRegExEvents** ppsrree)
{
    *ppsrree = nullptr;
    CMockPowerRenameRegExEvents* psrree = new CMockPowerRenameRegExEvents();
    HRESULT hr = E_OUTOFMEMORY;
    if (psrree)
    {
        hr = psrree->QueryInterface(IID_PPV_ARGS(ppsrree));
        psrree->Release();
    }
    return hr;
}
