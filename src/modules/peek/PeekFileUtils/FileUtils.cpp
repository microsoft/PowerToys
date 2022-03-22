#include "pch.h"
#include "FileUtils.h"

#include <common/logger/logger.h>

#include <shlobj.h>
#include <shlwapi.h>
#include <WinUser.h>

namespace FileUtils
{
    HRESULT GetSelectedFile(String& filepath)
    {
        HRESULT hr = S_FALSE;

        TCHAR szPath[MAX_PATH];
        TCHAR szItem[MAX_PATH];

        // The window handle the user interacted with
        HWND hwndFind = GetForegroundWindow();

        // To find the file the user is focused on in FE:
        //  1. Find all active explorer windows
        //  2. See if we can find the user interacted window within it
        //  3. Find the focused file in that explorer window
        IShellWindows* psw;
        if (SUCCEEDED(CoCreateInstance(CLSID_ShellWindows, NULL, CLSCTX_ALL, IID_IShellWindows, (void**)&psw)))
        {
            VARIANT v;
            V_VT(&v) = VT_I4;
            IDispatch* pdisp;

            BOOL isExplorerWindow = FALSE;
            for (V_I4(&v) = 0; !isExplorerWindow && psw->Item(v, &pdisp) == S_OK; V_I4(&v)++)
            {
                IWebBrowserApp* pwba;
                if (SUCCEEDED(pdisp->QueryInterface(IID_IWebBrowserApp, (void**)&pwba)))
                {
                    HWND hwndWBA;
                    if (SUCCEEDED(pwba->get_HWND((LONG_PTR*)&hwndWBA)) && hwndWBA == hwndFind)
                    {
                        isExplorerWindow = TRUE;
                        IServiceProvider* psp;

                        if (SUCCEEDED(pwba->QueryInterface(IID_IServiceProvider, (void**)&psp)))
                        {
                            IShellBrowser* psb;
                            if (SUCCEEDED(psp->QueryService(SID_STopLevelBrowser, IID_IShellBrowser, (void**)&psb)))
                            {
                                IShellView* psv;
                                if (SUCCEEDED(psb->QueryActiveShellView(&psv)))
                                {
                                    IFolderView* pfv;
                                    if (SUCCEEDED(psv->QueryInterface(IID_IFolderView, (void**)&pfv)))
                                    {
                                        IPersistFolder2* ppf2;
                                        if (SUCCEEDED(pfv->GetFolder(IID_IPersistFolder2, (void**)&ppf2)))
                                        {
                                            LPITEMIDLIST pidlFolder;
                                            if (SUCCEEDED(ppf2->GetCurFolder(&pidlFolder)))
                                            {
                                                if (!SHGetPathFromIDList(pidlFolder, szPath))
                                                {
                                                    lstrcpyn(szPath, TEXT("<not a directory>"), MAX_PATH);
                                                }

                                                int iFocus;
                                                if (SUCCEEDED(pfv->GetFocusedItem(&iFocus)))
                                                {
                                                    LPITEMIDLIST pidlItem;
                                                    if (SUCCEEDED(pfv->Item(iFocus, &pidlItem)))
                                                    {
                                                        IShellFolder* psf;
                                                        if (SUCCEEDED(ppf2->QueryInterface(IID_IShellFolder, (void**)&psf)))
                                                        {
                                                            STRRET str;
                                                            if (SUCCEEDED(psf->GetDisplayNameOf(pidlItem, SHGDN_INFOLDER, &str)))
                                                            {
                                                                StrRetToBuf(&str, pidlItem, szItem, MAX_PATH);

                                                                filepath.append(szPath);
                                                                filepath.append(L"\\");
                                                                filepath.append(szItem);

                                                                hr = S_OK;
                                                            }
                                                            psf->Release();
                                                        }
                                                        CoTaskMemFree(pidlItem);
                                                    }
                                                }
                                                CoTaskMemFree(pidlFolder);
                                            }
                                            ppf2->Release();
                                        }
                                        pfv->Release();
                                    }
                                    psv->Release();
                                }
                                psb->Release();
                            }
                            psp->Release();
                        }
                    }
                    pwba->Release();
                }
                pdisp->Release();
            }
            psw->Release();
        }

        return hr;
    }
}