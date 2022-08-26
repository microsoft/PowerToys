#include "pch.h"

#include "BGRATextureView.h"

#if defined(DEBUG_TEXTURE)
void BGRATextureView::SaveAsBitmap(const char* filename) const
{
    wil::unique_hbitmap bitmap{ CreateBitmap(static_cast<int>(pitch), static_cast<int>(height), 1, 32, pixels) };
    const HBITMAP hBitmap = bitmap.get();
    DWORD dwPaletteSize = 0, dwBmBitsSize = 0, dwDIBSize = 0, dwWritten = 0;
    LPBITMAPINFOHEADER lpBitmapInfo;
    HANDLE hDib, hPal, hOldPal2 = NULL;
    HDC hDC = CreateDC(TEXT("DISPLAY"), NULL, NULL, NULL);
    const int iBits = GetDeviceCaps(hDC, BITSPIXEL) * GetDeviceCaps(hDC, PLANES);
    DeleteDC(hDC);
    WORD wBitCount = 24;
    if (iBits <= 1)
        wBitCount = 1;
    else if (iBits <= 4)
        wBitCount = 4;
    else if (iBits <= 8)
        wBitCount = 8;

    BITMAP Bitmap0;
    GetObject(hBitmap, sizeof(Bitmap0), (LPSTR)&Bitmap0);
    BITMAPINFOHEADER bi = {};
    bi.biSize = sizeof(BITMAPINFOHEADER);
    bi.biWidth = Bitmap0.bmWidth;
    bi.biHeight = -Bitmap0.bmHeight;
    bi.biPlanes = 1;
    bi.biBitCount = wBitCount;
    bi.biCompression = BI_RGB;
    bi.biClrUsed = 256;
    dwBmBitsSize = ((Bitmap0.bmWidth * wBitCount + 31) & ~31) / 8 * Bitmap0.bmHeight;
    hDib = GlobalAlloc(GHND, dwBmBitsSize + dwPaletteSize + sizeof(BITMAPINFOHEADER));
    lpBitmapInfo = (LPBITMAPINFOHEADER)GlobalLock(hDib);
    *lpBitmapInfo = bi;

    hPal = GetStockObject(DEFAULT_PALETTE);
    if (hPal)
    {
        hDC = GetDC(NULL);
        hOldPal2 = SelectPalette(hDC, (HPALETTE)hPal, FALSE);
        RealizePalette(hDC);
    }

    GetDIBits(hDC, hBitmap, 0, (UINT)Bitmap0.bmHeight, (LPSTR)lpBitmapInfo + sizeof(BITMAPINFOHEADER) + dwPaletteSize, (BITMAPINFO*)lpBitmapInfo, DIB_RGB_COLORS);

    if (hOldPal2)
    {
        SelectPalette(hDC, (HPALETTE)hOldPal2, TRUE);
        RealizePalette(hDC);
        ReleaseDC(NULL, hDC);
    }

    wil::unique_handle fh{ CreateFileA(filename, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_SEQUENTIAL_SCAN, NULL) };

    if (!fh)
        return;

    BITMAPFILEHEADER bitmapFileHeader = {};
    bitmapFileHeader.bfType = 0x4D42; // "BM"
    dwDIBSize = sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER) + dwPaletteSize + dwBmBitsSize;
    bitmapFileHeader.bfSize = dwDIBSize;
    bitmapFileHeader.bfOffBits = (DWORD)sizeof(BITMAPFILEHEADER) + (DWORD)sizeof(BITMAPINFOHEADER) + dwPaletteSize;

    WriteFile(fh.get(), (LPSTR)&bitmapFileHeader, sizeof(BITMAPFILEHEADER), &dwWritten, NULL);

    WriteFile(fh.get(), (LPSTR)lpBitmapInfo, dwDIBSize, &dwWritten, NULL);
    GlobalUnlock(hDib);
    GlobalFree(hDib);
}
#endif