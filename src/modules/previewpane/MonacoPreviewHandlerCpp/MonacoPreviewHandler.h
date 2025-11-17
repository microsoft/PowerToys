#pragma once

#include "pch.h"

#include <filesystem>
#include <ShlObj.h>
#include <string>

class MonacoPreviewHandler :
    public IInitializeWithFile,
    public IPreviewHandler,
    public IPreviewHandlerVisuals,
    public IOleWindow,
    public IObjectWithSite
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IInitializeWithFile
    IFACEMETHODIMP Initialize(LPCWSTR pszFilePath, DWORD grfMode);

    // IPreviewHandler
    IFACEMETHODIMP SetWindow(HWND hwnd, const RECT* prc);
    IFACEMETHODIMP SetFocus();
    IFACEMETHODIMP QueryFocus(HWND* phwnd);
    IFACEMETHODIMP TranslateAccelerator(MSG* pmsg);
    IFACEMETHODIMP SetRect(const RECT* prc);
    IFACEMETHODIMP DoPreview();
    IFACEMETHODIMP Unload();

    // IPreviewHandlerVisuals
    IFACEMETHODIMP SetBackgroundColor(COLORREF color);
    IFACEMETHODIMP SetFont(const LOGFONTW* plf);
    IFACEMETHODIMP SetTextColor(COLORREF color);

    // IOleWindow
    IFACEMETHODIMP GetWindow(HWND* phwnd);
    IFACEMETHODIMP ContextSensitiveHelp(BOOL fEnterMode);

    // IObjectWithSite
    IFACEMETHODIMP SetSite(IUnknown* punkSite);
    IFACEMETHODIMP GetSite(REFIID riid, void** ppv);

    MonacoPreviewHandler();
protected:
    ~MonacoPreviewHandler();

private:
    // Reference count of component.
    long m_cRef;

    // Provided during initialization.
    std::wstring m_filePath;
    
    // Parent window that hosts the previewer window.
    // Note: do NOT DestroyWindow this.
    HWND m_hwndParent;
    // Bounding rect of the parent window.
    RECT m_rcParent;

    // Site pointer from host, used to get IPreviewHandlerFrame.
    IUnknown* m_punkSite;

    HANDLE m_process;

    HANDLE m_resizeEvent;
};