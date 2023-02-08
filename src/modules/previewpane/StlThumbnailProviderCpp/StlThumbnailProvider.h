#pragma once

#include "pch.h"

#include <ShlObj.h>
#include <string>
#include <thumbcache.h>

class StlThumbnailProvider :
    public IInitializeWithStream,
    public IThumbnailProvider
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IInitializeWithStream
    IFACEMETHODIMP Initialize(IStream* pstream, DWORD grfMode);

    // IThumbnailProvider
    IFACEMETHODIMP GetThumbnail(UINT cx, HBITMAP* phbmp, WTS_ALPHATYPE* pdwAlpha);

    StlThumbnailProvider();
protected:
    ~StlThumbnailProvider();

private:
    // Reference count of component.
    long m_cRef;

    // Provided during initialization.
    IStream* m_pStream;

    HANDLE m_process;
};