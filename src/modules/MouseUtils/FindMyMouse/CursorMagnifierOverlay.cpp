#include "pch.h"
#include "CursorMagnifierOverlay.h"

#include <algorithm>
#include <cmath>
#include <cstring>

CursorMagnifierOverlay::~CursorMagnifierOverlay()
{
    DestroyWindowInternal();
}

bool CursorMagnifierOverlay::Initialize(HINSTANCE instance)
{
    if (m_hwnd)
    {
        return true;
    }

    m_instance = instance;

    WNDCLASS wc{};
    if (!GetClassInfoW(instance, kWindowClassName, &wc))
    {
        wc.lpfnWndProc = WndProc;
        wc.hInstance = instance;
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(NULL_BRUSH));
        wc.lpszClassName = kWindowClassName;

        if (!RegisterClassW(&wc))
        {
            Logger::error("RegisterClassW failed for cursor magnifier. GetLastError={}", GetLastError());
            return false;
        }
    }

    DWORD exStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
    m_hwnd = CreateWindowExW(
        exStyle,
        kWindowClassName,
        L"PowerToys FindMyMouse Cursor Magnifier",
        WS_POPUP,
        0,
        0,
        0,
        0,
        nullptr,
        nullptr,
        instance,
        this);

    if (!m_hwnd)
    {
        Logger::error("CreateWindowExW failed for cursor magnifier. GetLastError={}", GetLastError());
        return false;
    }

    return true;
}

void CursorMagnifierOverlay::Terminate()
{
    if (!m_hwnd)
    {
        return;
    }

    PostMessage(m_hwnd, WM_CLOSE, 0, 0);
}

void CursorMagnifierOverlay::SetVisible(bool visible)
{
    if (!m_hwnd || m_visible == visible)
    {
        return;
    }

    m_visible = visible;
    if (visible)
    {
        SetTimer(m_hwnd, kTimerId, kFrameIntervalMs, nullptr);
        ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
        SetWindowPos(m_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        Render();
    }
    else
    {
        KillTimer(m_hwnd, kTimerId);
        ShowWindow(m_hwnd, SW_HIDE);
    }
}

void CursorMagnifierOverlay::SetScale(float scale)
{
    if (scale > 0.0f)
    {
        m_scale = scale;
    }
}

LRESULT CALLBACK CursorMagnifierOverlay::WndProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    CursorMagnifierOverlay* self = nullptr;
    if (message == WM_NCCREATE)
    {
        auto create = reinterpret_cast<LPCREATESTRUCT>(lParam);
        self = static_cast<CursorMagnifierOverlay*>(create->lpCreateParams);
        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        self->m_hwnd = hwnd;
    }
    else
    {
        self = reinterpret_cast<CursorMagnifierOverlay*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    }

    if (!self)
    {
        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    switch (message)
    {
    case WM_TIMER:
        if (wParam == kTimerId)
        {
            self->OnTimer();
        }
        return 0;
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_DESTROY:
        self->CleanupResources();
        return 0;
    }

    return DefWindowProc(hwnd, message, wParam, lParam);
}

void CursorMagnifierOverlay::OnTimer()
{
    if (!m_visible)
    {
        return;
    }

    Render();
}

void CursorMagnifierOverlay::Render()
{
    if (!m_hwnd)
    {
        return;
    }

    CURSORINFO ci{};
    ci.cbSize = sizeof(ci);
    if (!GetCursorInfo(&ci) || (ci.flags & CURSOR_SHOWING) == 0)
    {
        ShowWindow(m_hwnd, SW_HIDE);
        return;
    }

    ICONINFO ii{};
    if (!GetIconInfo(ci.hCursor, &ii))
    {
        return;
    }

    int srcW = 0;
    int srcH = 0;
    if (ii.hbmColor)
    {
        BITMAP bm{};
        GetObject(ii.hbmColor, sizeof(bm), &bm);
        srcW = bm.bmWidth;
        srcH = bm.bmHeight;
    }
    else if (ii.hbmMask)
    {
        BITMAP bm{};
        GetObject(ii.hbmMask, sizeof(bm), &bm);
        srcW = bm.bmWidth;
        srcH = bm.bmHeight / 2;
    }

    if (ii.hbmColor)
    {
        DeleteObject(ii.hbmColor);
    }
    if (ii.hbmMask)
    {
        DeleteObject(ii.hbmMask);
    }

    if (srcW <= 0 || srcH <= 0)
    {
        srcW = GetSystemMetrics(SM_CXCURSOR);
        srcH = GetSystemMetrics(SM_CYCURSOR);
    }

    const int dstW = (std::max)(1, static_cast<int>(std::lround(srcW * m_scale)));
    const int dstH = (std::max)(1, static_cast<int>(std::lround(srcH * m_scale)));

    EnsureResources(dstW, dstH);
    if (!m_memDc || !m_bits)
    {
        return;
    }

    std::memset(m_bits, 0, static_cast<size_t>(dstW) * static_cast<size_t>(dstH) * 4);
    DrawIconEx(m_memDc, 0, 0, ci.hCursor, dstW, dstH, 0, nullptr, DI_NORMAL);

    const int x = static_cast<int>(std::lround(ci.ptScreenPos.x - ii.xHotspot * m_scale));
    const int y = static_cast<int>(std::lround(ci.ptScreenPos.y - ii.yHotspot * m_scale));
    POINT ptDst{ x, y };
    POINT ptSrc{ 0, 0 };
    SIZE size{ dstW, dstH };
    BLENDFUNCTION blend{};
    blend.BlendOp = AC_SRC_OVER;
    blend.SourceConstantAlpha = 255;
    blend.AlphaFormat = AC_SRC_ALPHA;

    UpdateLayeredWindow(m_hwnd, nullptr, &ptDst, &size, m_memDc, &ptSrc, 0, &blend, ULW_ALPHA);
    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
}

void CursorMagnifierOverlay::EnsureResources(int width, int height)
{
    if (width <= 0 || height <= 0)
    {
        return;
    }

    if (m_dib && m_dibSize.cx == width && m_dibSize.cy == height)
    {
        return;
    }

    CleanupResources();

    m_memDc = CreateCompatibleDC(nullptr);
    if (!m_memDc)
    {
        return;
    }

    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = width;
    bmi.bmiHeader.biHeight = -height; // top-down DIB
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    m_dib = CreateDIBSection(m_memDc, &bmi, DIB_RGB_COLORS, &m_bits, nullptr, 0);
    if (!m_dib || !m_bits)
    {
        CleanupResources();
        return;
    }

    SelectObject(m_memDc, m_dib);
    m_dibSize = { width, height };
}

void CursorMagnifierOverlay::CleanupResources()
{
    if (m_dib)
    {
        DeleteObject(m_dib);
        m_dib = nullptr;
    }
    if (m_memDc)
    {
        DeleteDC(m_memDc);
        m_memDc = nullptr;
    }
    m_bits = nullptr;
    m_dibSize = { 0, 0 };
}

void CursorMagnifierOverlay::DestroyWindowInternal()
{
    if (m_hwnd)
    {
        DestroyWindow(m_hwnd);
        m_hwnd = nullptr;
    }
    CleanupResources();
}
