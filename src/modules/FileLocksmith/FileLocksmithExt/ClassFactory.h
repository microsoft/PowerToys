#pragma once

#include "pch.h"

class ClassFactory : public IClassFactory
{
public:
    ClassFactory(_In_ REFCLSID clsid);
    ~ClassFactory();

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override;
    IFACEMETHODIMP LockServer(BOOL fLock) override;
private:
    std::atomic<ULONG> m_ref_count;
    IID m_clsid;
};
