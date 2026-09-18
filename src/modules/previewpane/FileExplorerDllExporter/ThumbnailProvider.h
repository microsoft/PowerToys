#pragma once

#include "pch.h"

#include <ShlObj.h>
#include <string>
#include <thumbcache.h>

class ThumbnailProvider :
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

    ThumbnailProvider(std::string name, std::wstring logFilePath, std::wstring exeName, std::wstring tempFolderName, std::wstring extension);
protected:
    ~ThumbnailProvider();

private:
    // Reference count of component.
    long m_cRef;

    // Provided during initialization.
    IStream* m_pStream;

    HANDLE m_process;

    std::wstring m_exeName;
    std::wstring m_tempFolderName;
    std::wstring m_extension;
};
