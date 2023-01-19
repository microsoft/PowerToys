#include "icon_helpers.h"
#include <shellapi.h>
#include <shlwapi.h>
#include <commctrl.h>

HRESULT GetIconIndexFromPath(_In_ PCWSTR path, _Out_ int* index)
{
    *index = 0;

    HRESULT hr = E_FAIL;

    SHFILEINFO shFileInfo = { 0 };

    if (!PathIsRelative(path))
    {
        DWORD attrib = GetFileAttributes(path);
        auto himl =SHGetFileInfo(path, attrib, &shFileInfo, sizeof(shFileInfo), (SHGFI_SYSICONINDEX | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES));
        if (himl)
        {
            *index = shFileInfo.iIcon;
            // We shouldn't free the HIMAGELIST.
            hr = S_OK;
        }
    }

    return hr;
}

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
            HBITMAP hBitmapOld = static_cast<HBITMAP>(SelectObject(hDC, hBitmap));
            if (hBitmapOld != NULL)
            {
                // Draw icon into DC
                if (DrawIconEx(hDC, 0, 0, hIcon, rc.right, rc.bottom, 0, NULL, DI_NORMAL))
                {
                    // Restore original bitmap in DC
                    hBitmapResult = static_cast<HBITMAP>(SelectObject(hDC, hBitmapOld));
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