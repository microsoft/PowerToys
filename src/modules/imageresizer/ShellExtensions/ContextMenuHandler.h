#pragma once

#define ID_RESIZE_PICTURES 0
#define RESIZE_PICTURES_VERBW L"resize"
#include "stdafx.h"
#include "resource.h"
#include "ShellExtensions_i.h"

#if defined(_WIN32_WCE) && !defined(_CE_DCOM) && !defined(_CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA)
#error "Single-threaded COM objects are not properly supported on Windows CE platform, such as the Windows Mobile platforms that do not include full DCOM support. Define _CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA to force ATL to support creating single-thread COM object's and allow use of it's single-threaded COM object implementations. The threading model in your rgs file was set to 'Free' as that is the only threading model supported in non DCOM Windows CE platforms."
#endif

using namespace ATL;

class ATL_NO_VTABLE CContextMenuHandler :
    public CComObjectRootEx<CComSingleThreadModel>,
    public CComCoClass<CContextMenuHandler, &CLSID_ContextMenuHandler>,
    public IShellExtInit,
    public IContextMenu
{
    BEGIN_COM_MAP(CContextMenuHandler)
    COM_INTERFACE_ENTRY(IShellExtInit)
    COM_INTERFACE_ENTRY(IContextMenu)
    END_COM_MAP()
    DECLARE_REGISTRY_RESOURCEID(IDR_CONTEXTMENUHANDLER)
    DECLARE_NOT_AGGREGATABLE(CContextMenuHandler)

public:
    CContextMenuHandler();
    ~CContextMenuHandler();
    HRESULT STDMETHODCALLTYPE Initialize(_In_opt_ PCIDLIST_ABSOLUTE pidlFolder, _In_opt_ IDataObject* pdtobj, _In_opt_ HKEY hkeyProgID);
    HRESULT STDMETHODCALLTYPE QueryContextMenu(_In_ HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags);
    HRESULT STDMETHODCALLTYPE GetCommandString(UINT_PTR idCmd, UINT uType, _In_ UINT* pReserved, LPSTR pszName, UINT cchMax);
    HRESULT STDMETHODCALLTYPE InvokeCommand(_In_ CMINVOKECOMMANDINFO* pici);

private:
    void Uninitialize();
    HRESULT ResizePictures(CMINVOKECOMMANDINFO* pici);
    PCIDLIST_ABSOLUTE m_pidlFolder;
    IDataObject* m_pdtobj;
    HBITMAP m_hbmpIcon = nullptr;
};

OBJECT_ENTRY_AUTO(__uuidof(ContextMenuHandler), CContextMenuHandler)

// From Helpers.cpp in PowerRenameLib
HBITMAP CreateBitmapFromIcon(_In_ HICON hIcon, _In_opt_ UINT width, _In_opt_ UINT height)
{
    HBITMAP hBitmapResult = NULL;

    // Create compatible DC
    HDC hDC = CreateCompatibleDC(NULL);
    if (hDC != NULL)
    {
        // Get bitmap rectangle size
        RECT rc = { 0 };
        rc.left = 0;
        rc.right = (width != 0) ? width : GetSystemMetrics(SM_CXSMICON);
        rc.top = 0;
        rc.bottom = (height != 0) ? height : GetSystemMetrics(SM_CYSMICON);

        // Create bitmap compatible with DC
        BITMAPINFO BitmapInfo;
        ZeroMemory(&BitmapInfo, sizeof(BITMAPINFO));

        BitmapInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        BitmapInfo.bmiHeader.biWidth = rc.right;
        BitmapInfo.bmiHeader.biHeight = rc.bottom;
        BitmapInfo.bmiHeader.biPlanes = 1;
        BitmapInfo.bmiHeader.biBitCount = 32;
        BitmapInfo.bmiHeader.biCompression = BI_RGB;

        HDC hDCBitmap = GetDC(NULL);

        HBITMAP hBitmap = CreateDIBSection(hDCBitmap, &BitmapInfo, DIB_RGB_COLORS, NULL, NULL, 0);

        ReleaseDC(NULL, hDCBitmap);

        if (hBitmap != NULL)
        {
            // Select bitmap into DC
            HBITMAP hBitmapOld = (HBITMAP)SelectObject(hDC, hBitmap);
            if (hBitmapOld != NULL)
            {
                // Draw icon into DC
                if (DrawIconEx(hDC, 0, 0, hIcon, rc.right, rc.bottom, 0, NULL, DI_NORMAL))
                {
                    // Restore original bitmap in DC
                    hBitmapResult = (HBITMAP)SelectObject(hDC, hBitmapOld);
                    hBitmapOld = NULL;
                    hBitmap = NULL;
                }

                if (hBitmapOld != NULL)
                {
                    SelectObject(hDC, hBitmapOld);
                }
            }

            if (hBitmap != NULL)
            {
                DeleteObject(hBitmap);
            }
        }

        DeleteDC(hDC);
    }

    return hBitmapResult;
}