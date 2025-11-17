#pragma once

#include <Unknwn.h>
#include <winrt/base.h>
#include <atomic>
#include <shlwapi.h>

template<typename T>
class com_object_factory : public IClassFactory
{
public:
    HRESULT __stdcall QueryInterface(const IID& riid, void** ppv) override
    {
        static const QITAB qit[] = {
            QITABENT(com_object_factory, IClassFactory),
            { 0 }
        };
        return QISearch(this, qit, riid, ppv);
    }

    ULONG __stdcall AddRef() override
    {
        return ++_refCount;
    }

    ULONG __stdcall Release() override
    {
        LONG refCount = --_refCount;
        return refCount;
    }

    HRESULT __stdcall CreateInstance(IUnknown* punkOuter, const IID& riid, void** ppv)
    {
        *ppv = nullptr;
                
        if (punkOuter)
        {
            return CLASS_E_NOAGGREGATION;
        }

        T* psrm = new (std::nothrow) T();
        HRESULT hr = psrm ? S_OK : E_OUTOFMEMORY;
        if (SUCCEEDED(hr))
        {
            hr = psrm->QueryInterface(riid, ppv);
            psrm->Release();
        }
        return hr;
    }

    HRESULT __stdcall LockServer(BOOL)
    {
        return S_OK;
    }

private:
    std::atomic<long> _refCount;
};