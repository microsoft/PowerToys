#include "pch.h"

#include "ClassFactory.h"
#include "ExplorerCommand.h"

// Implementations of inherited IUnknown methods

IFACEMETHODIMP ClassFactory::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(ClassFactory, IClassFactory),
        { 0, 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG) ClassFactory::AddRef()
{
    return ++m_ref_count;
}

IFACEMETHODIMP_(ULONG) ClassFactory::Release()
{
    auto result = --m_ref_count;
    if (result == 0)
    {
        delete this;
    }
    return result;
}

// Implementations of inherited IClassFactory methods

IFACEMETHODIMP ClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject)
{
    *ppvObject = NULL;
    HRESULT hr;
    if (pUnkOuter)
    {
        hr = CLASS_E_NOAGGREGATION;
    }
    else if (m_clsid == __uuidof(ExplorerCommand))
    {
        hr = ExplorerCommand::s_CreateInstance(pUnkOuter, riid, ppvObject);
    }
    else
    {
        hr = CLASS_E_CLASSNOTAVAILABLE;
    }
    return hr;
}
