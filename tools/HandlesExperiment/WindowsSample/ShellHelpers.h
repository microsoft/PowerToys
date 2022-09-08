// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved

#pragma once

#define STRICT_TYPED_ITEMIDS    // in case you do IDList stuff you want this on for better type saftey
#define UNICODE 1

#include <windows.h>
#include <windowsx.h>           // for WM_COMMAND handling macros
#include <shlobj.h>             // shell stuff
#include <shlwapi.h>            // QISearch, easy way to implement QI
#include <propkey.h>
#include <propvarutil.h>
#include <strsafe.h>
#include <objbase.h>

#pragma comment(lib, "shlwapi.lib")     // link to this
#pragma comment(lib, "comctl32.lib")    // link to this
#pragma comment(lib, "propsys.lib")     // link to this

// set up common controls v6 the easy way
#pragma comment(linker,"/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

__inline HRESULT ResultFromKnownLastError() { const DWORD err = GetLastError(); return err == ERROR_SUCCESS ? E_FAIL : HRESULT_FROM_WIN32(err); }

// map Win32 APIs that follow the return BOOL/set last error protocol
// into HRESULT
//
// example: MoveFileEx()

__inline HRESULT ResultFromWin32Bool(BOOL b)
{
    return b ? S_OK : ResultFromKnownLastError();
}

#if (NTDDI_VERSION >= NTDDI_VISTA)

__inline HRESULT ShellExecuteItem(HWND hwnd, PCWSTR pszVerb, IShellItem *psi)
{
    // how to activate a shell item, use ShellExecute().
    PIDLIST_ABSOLUTE pidl;
    HRESULT hr = SHGetIDListFromObject(psi, &pidl);
    if (SUCCEEDED(hr))
    {
        SHELLEXECUTEINFO ei = { sizeof(ei) };
        ei.fMask = SEE_MASK_INVOKEIDLIST;
        ei.hwnd = hwnd;
        ei.nShow = SW_NORMAL;
        ei.lpIDList = pidl;
        ei.lpVerb = pszVerb;

        hr = ResultFromWin32Bool(ShellExecuteEx(&ei));

        CoTaskMemFree(pidl);
    }
    return hr;
}

__inline HRESULT GetItemFromView(IFolderView2 *pfv, int iItem, REFIID riid, void **ppv)
{
    *ppv = NULL;

    HRESULT hr = S_OK;

    if (iItem == -1)
    {
        hr = pfv->GetSelectedItem(-1, &iItem); // Returns S_FALSE if none selected
    }

    if (S_OK == hr)
    {
        hr = pfv->GetItem(iItem, riid, ppv);
    }
    else
    {
        hr = E_FAIL;
    }
    return hr;
}

// set the icon for your window using WM_SETICON from one of the set of stock system icons
// caller must call ClearDialogIcon() to free the HICON that is created
__inline void SetDialogIcon(HWND hdlg, SHSTOCKICONID siid)
{
    SHSTOCKICONINFO sii = {sizeof(sii)};
    if (SUCCEEDED(SHGetStockIconInfo(siid, SHGFI_ICON | SHGFI_SMALLICON, &sii)))
    {
        SendMessage(hdlg, WM_SETICON, ICON_SMALL, (LPARAM) sii.hIcon);
    }
    if (SUCCEEDED(SHGetStockIconInfo(siid, SHGFI_ICON | SHGFI_LARGEICON, &sii)))
    {
        SendMessage(hdlg, WM_SETICON, ICON_BIG, (LPARAM) sii.hIcon);
    }
}
#endif

// free the HICON that was set using SetDialogIcon()
__inline void ClearDialogIcon(HWND hdlg)
{
    DestroyIcon((HICON)SendMessage(hdlg, WM_GETICON, ICON_SMALL, 0));
    DestroyIcon((HICON)SendMessage(hdlg, WM_GETICON, ICON_BIG, 0));
}

__inline HRESULT SetItemImageImageInStaticControl(HWND hwndStatic, IShellItem *psi)
{
    HBITMAP hbmp = NULL;
    HRESULT hr = S_OK;
    if (psi)
    {
        IShellItemImageFactory *psiif;
        hr = psi->QueryInterface(IID_PPV_ARGS(&psiif));
        if (SUCCEEDED(hr))
        {
            RECT rc;
            GetWindowRect(hwndStatic, &rc);
            const LONG dxdy = min(rc.right - rc.left, rc.bottom - rc.top);    // make it square
            const SIZE size = { dxdy, dxdy };

            hr = psiif->GetImage(size, SIIGBF_RESIZETOFIT, &hbmp);
            psiif->Release();
        }
    }

    if (SUCCEEDED(hr))
    {
        HGDIOBJ hgdiOld = (HGDIOBJ) SendMessage(hwndStatic, STM_SETIMAGE, (WPARAM) IMAGE_BITMAP, (LPARAM) hbmp);
        if (hgdiOld)
        {
            DeleteObject(hgdiOld);  // if there was an old one clean it up
        }
    }

    return hr;
}


__inline HRESULT SHILCloneFull(PCUIDLIST_ABSOLUTE pidl, PIDLIST_ABSOLUTE *ppidl)
{
    *ppidl = ILCloneFull(pidl);
    return *ppidl ? S_OK : E_OUTOFMEMORY;
}

__inline HRESULT SHILClone(PCUIDLIST_RELATIVE pidl, PIDLIST_RELATIVE *ppidl)
{
    *ppidl = ILClone(pidl);
    return *ppidl ? S_OK : E_OUTOFMEMORY;
}

__inline HRESULT SHILCombine(PCIDLIST_ABSOLUTE pidl1, PCUIDLIST_RELATIVE pidl2, PIDLIST_ABSOLUTE *ppidl)
{
    *ppidl = ILCombine(pidl1, pidl2);
    return *ppidl ? S_OK : E_OUTOFMEMORY;
}

__inline HRESULT GetItemAt(IShellItemArray *psia, DWORD i, REFIID riid, void **ppv)
{
    *ppv = NULL;
    IShellItem *psi = NULL;     // avoid error C4701
    HRESULT hr = psia ? psia->GetItemAt(i, &psi) : E_NOINTERFACE;
    if (SUCCEEDED(hr))
    {
        hr = psi->QueryInterface(riid, ppv);
        psi->Release();
    }
    return hr;
}

#define MAP_ENTRY(x) {L#x, x}

__inline HRESULT ShellAttributesToString(SFGAOF sfgaof, PWSTR *ppsz)
{
    *ppsz = NULL;

    static const struct { PCWSTR pszName; SFGAOF sfgaof; } c_rgItemAttributes[] =
    {
        // note, SFGAO_HASSUBFOLDER is too expesnive to compute
        // and has been excluded from this list
        MAP_ENTRY(SFGAO_STREAM),
        MAP_ENTRY(SFGAO_FOLDER),
        MAP_ENTRY(SFGAO_FILESYSTEM),
        MAP_ENTRY(SFGAO_FILESYSANCESTOR),
        MAP_ENTRY(SFGAO_STORAGE),
        MAP_ENTRY(SFGAO_STORAGEANCESTOR),
        MAP_ENTRY(SFGAO_LINK),
        MAP_ENTRY(SFGAO_CANCOPY),
        MAP_ENTRY(SFGAO_CANMOVE),
        MAP_ENTRY(SFGAO_CANLINK),
        MAP_ENTRY(SFGAO_CANRENAME),
        MAP_ENTRY(SFGAO_CANDELETE),
        MAP_ENTRY(SFGAO_HASPROPSHEET),
        MAP_ENTRY(SFGAO_DROPTARGET),
        MAP_ENTRY(SFGAO_ENCRYPTED),
        MAP_ENTRY(SFGAO_ISSLOW),
        MAP_ENTRY(SFGAO_GHOSTED),
        MAP_ENTRY(SFGAO_SHARE),
        MAP_ENTRY(SFGAO_READONLY),
        MAP_ENTRY(SFGAO_HIDDEN),
        MAP_ENTRY(SFGAO_REMOVABLE),
        MAP_ENTRY(SFGAO_COMPRESSED),
        MAP_ENTRY(SFGAO_BROWSABLE),
        MAP_ENTRY(SFGAO_NONENUMERATED),
        MAP_ENTRY(SFGAO_NEWCONTENT),
    };

    WCHAR sz[512] = {};
    PWSTR psz = sz;
    size_t cch = ARRAYSIZE(sz);

    StringCchPrintfEx(psz, cch, &psz, &cch, 0, L"0x%08X", sfgaof);

    for (int i = 0; i < ARRAYSIZE(c_rgItemAttributes); i++)
    {
        if (c_rgItemAttributes[i].sfgaof & sfgaof)
        {
            StringCchPrintfEx(psz, cch, &psz, &cch, 0, L", %s", c_rgItemAttributes[i].pszName);
        }
    }
    return SHStrDup(sz, ppsz);
}

template <class T> void SafeRelease(T **ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = NULL;
    }
}

// assign an interface pointer, release old, capture ref to new, can be used to set to zero too

template <class T> HRESULT SetInterface(T **ppT, IUnknown *punk)
{
    SafeRelease(ppT);
    return punk ? punk->QueryInterface(ppT) : E_NOINTERFACE;
}

// remote COM methods are dispatched in the context of an exception handler that consumes
// all SEH exceptions including crahses and C++ exceptions. this is undesirable as it
// means programs will continue to run after such an exception has been thrown,
// leaving the process in a inconsistent state.
//
// this applies to COM methods like IDropTarget::Drop()
//
// this code turns off that behavior

__inline void DisableComExceptionHandling()
{
    IGlobalOptions *pGlobalOptions;
    HRESULT hr =  CoCreateInstance(CLSID_GlobalOptions, NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&pGlobalOptions));
    if (SUCCEEDED(hr))
    {
#if (NTDDI_VERSION >= NTDDI_WIN7)
        hr = pGlobalOptions->Set(COMGLB_EXCEPTION_HANDLING, COMGLB_EXCEPTION_DONOT_HANDLE_ANY);
#else
        hr = pGlobalOptions->Set(COMGLB_EXCEPTION_HANDLING, COMGLB_EXCEPTION_DONOT_HANDLE);
#endif
        pGlobalOptions->Release();
    }
}

__inline void GetWindowRectInClient(HWND hwnd, RECT *prc)
{
    GetWindowRect(hwnd, prc);
    MapWindowPoints(GetDesktopWindow(), GetParent(hwnd), (POINT*)prc, 2);
}

// retrieve the HINSTANCE for the current DLL or EXE using this symbol that
// the linker provides for every module, avoids the need for a global HINSTANCE variable
// and provides access to this value for static libraries
EXTERN_C IMAGE_DOS_HEADER __ImageBase;
__inline HINSTANCE GetModuleHINSTANCE() { return (HINSTANCE)&__ImageBase; }
