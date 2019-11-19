#pragma once

struct Rect
{
    Rect() {}

    Rect(RECT rect) : m_rect(rect)
    {
    }

    Rect(RECT rect, UINT dpi) : m_rect(rect)
    {
        m_rect.right = m_rect.left + MulDiv(m_rect.right - m_rect.left, dpi, 96);
        m_rect.bottom = m_rect.top + MulDiv(m_rect.bottom - m_rect.top, dpi, 96);
    }

    int x() const { return m_rect.left; }
    int y() const { return m_rect.top; }
    int width() const { return m_rect.right - m_rect.left; }
    int height() const { return m_rect.bottom - m_rect.top; }
    int left() const { return m_rect.left; }
    int top() const { return m_rect.top; }
    int right() const { return m_rect.right; }
    int bottom() const { return m_rect.bottom; }
    int aspectRatio() const { return MulDiv(m_rect.bottom - m_rect.top, 100, m_rect.right - m_rect.left); }

private:
    RECT m_rect{};
};

inline void MakeWindowTransparent(HWND window)
{
    int const pos = -GetSystemMetrics(SM_CXVIRTUALSCREEN) - 8;
    if (wil::unique_hrgn hrgn{ CreateRectRgn(pos, 0, (pos + 1), 1) })
    {
        DWM_BLURBEHIND bh = { DWM_BB_ENABLE | DWM_BB_BLURREGION, TRUE, hrgn.get(), FALSE };
        DwmEnableBlurBehindWindow(window, &bh);
    }
}

inline void InitRGB(_Out_ RGBQUAD *quad, BYTE alpha, COLORREF color)
{
    ZeroMemory(quad, sizeof(*quad));
    quad->rgbReserved = alpha;
    quad->rgbRed = GetRValue(color) * alpha / 255;
    quad->rgbGreen = GetGValue(color) * alpha / 255;
    quad->rgbBlue = GetBValue(color) * alpha / 255;
}

inline void FillRectARGB(wil::unique_hdc& hdc, RECT const *prcFill, BYTE alpha, COLORREF color, bool blendAlpha)
{
    BITMAPINFO bi;
    ZeroMemory(&bi, sizeof(bi));
    bi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bi.bmiHeader.biWidth = 1;
    bi.bmiHeader.biHeight = 1;
    bi.bmiHeader.biPlanes = 1;
    bi.bmiHeader.biBitCount = 32;
    bi.bmiHeader.biCompression = BI_RGB;

    RECT fillRect;
    CopyRect(&fillRect, prcFill);
    if ((alpha == 255) || !blendAlpha)
    {
        // Opaque or the caller does not want to blend the alpha
        RGBQUAD bitmapBits;
        InitRGB(&bitmapBits, alpha, color);
        StretchDIBits(
            hdc.get(),
            fillRect.left,
            fillRect.top,
            fillRect.right - fillRect.left,
            fillRect.bottom - fillRect.top,
            0, 0, 1, 1, &bitmapBits, &bi, DIB_RGB_COLORS, SRCCOPY);
    }
    else
    {
        if (wil::unique_hdc hdcSrc{ CreateCompatibleDC(hdc.get()) })
        {
            void* pBitmapBits;
            if (wil::unique_hbitmap bitmapSource{ CreateDIBSection(hdcSrc.get(), &bi, DIB_RGB_COLORS, &pBitmapBits, nullptr, 0) })
            {
                InitRGB(reinterpret_cast<RGBQUAD *>(pBitmapBits), alpha, color);

                wil::unique_select_object bitmapOld{ SelectObject(hdcSrc.get(), bitmapSource.get()) };
                BLENDFUNCTION bf = { AC_SRC_OVER, 0, 255, AC_SRC_ALPHA };
                GdiAlphaBlend(
                    hdc.get(),
                    fillRect.left,
                    fillRect.top,
                    fillRect.right - fillRect.left,
                    fillRect.bottom - fillRect.top,
                    hdcSrc.get(), 0, 0, 1, 1, bf);
            }

        }
    }
}

inline void FrameRectARGB(wil::unique_hdc& hdc, const RECT &rc, BYTE bAlpha, COLORREF clr, int thickness)
{
    RECT sides[] = {
        { rc.left, rc.top, (rc.left + thickness), rc.bottom },
        { (rc.right - thickness), rc.top, rc.right, rc.bottom },
        { (rc.left + thickness), rc.top, (rc.right - thickness), (rc.top + thickness) },
        { (rc.left + thickness), (rc.bottom - thickness), (rc.right - thickness), rc.bottom }
    };

    for (UINT i = 0; i < ARRAYSIZE(sides); i++)
    {
        FillRectARGB(hdc, &(sides[i]), bAlpha, clr, false);
    }
}

inline bool ParseDeviceId(PCWSTR deviceId, PWSTR parsedId, size_t size)
{
    // We're interested in the unique part between the first and last #'s
    // Example input: \\?\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
    // Example output: DELA026#5&10a58c63&0&UID16777488 
    wchar_t buffer[256];
    StringCchCopy(buffer, 256, deviceId);

    PWSTR pszStart = wcschr(buffer, L'#');
    PWSTR pszEnd = wcsrchr(buffer, L'#');
    if (pszStart && pszEnd && (pszStart != pszEnd))
    {
        pszStart++; // skip past the first #
        *pszEnd = '\0';
        StringCchCopy(parsedId, size, pszStart);
        return true;
    }
    return false;
}
