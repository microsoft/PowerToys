#include "pch.h"
#include "CursorMagnifierOverlay.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <vector>

namespace
{
    // System cursor IDs (same values as OCR_* when OEMRESOURCE is defined).
    static constexpr UINT kCursorIdNormal = 32512;
    static const UINT kCursorIds[] = {
        kCursorIdNormal, // OCR_NORMAL
        32513, // OCR_IBEAM
        32514, // OCR_WAIT
        32515, // OCR_CROSS
        32516, // OCR_UP
        32642, // OCR_SIZENWSE
        32643, // OCR_SIZENESW
        32644, // OCR_SIZEWE
        32645, // OCR_SIZENS
        32646, // OCR_SIZEALL
        32648, // OCR_NO
        32649, // OCR_HAND
        32650, // OCR_APPSTARTING
        32651, // OCR_HELP
    };
}

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
        BeginScaleAnimation();
        HideSystemCursors();
        SetTimer(m_hwnd, kTimerId, kFrameIntervalMs, nullptr);
        ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
        SetWindowPos(m_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        Render();
    }
    else
    {
        KillTimer(m_hwnd, kTimerId);
        ShowWindow(m_hwnd, SW_HIDE);
        ResetCursorMetrics();
        m_animationStartTick = 0;
        RestoreSystemCursors();
    }
}

void CursorMagnifierOverlay::SetScale(float scale)
{
    if (scale > 0.0f && m_targetScale != scale)
    {
        m_targetScale = scale;
        if (m_visible)
        {
            m_startScale = m_currentScale;
            m_animationStartTick = GetTickCount64();
        }
    }
}

void CursorMagnifierOverlay::SetAnimationDurationMs(int durationMs)
{
    if (durationMs > 0)
    {
        m_animationDurationMs = static_cast<DWORD>(durationMs);
    }
    else
    {
        m_animationDurationMs = 1;
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

    HCURSOR cursorToDraw = ci.hCursor;
    if (m_systemCursorsHidden)
    {
        auto hiddenIt = m_hiddenCursorIds.find(ci.hCursor);
        if (hiddenIt != m_hiddenCursorIds.end())
        {
            auto originalIt = m_originalCursors.find(hiddenIt->second);
            if (originalIt != m_originalCursors.end() && originalIt->second)
            {
                cursorToDraw = originalIt->second;
            }
        }
    }

    UpdateCursorMetrics(cursorToDraw);

    const float scale = GetAnimatedScale();
    int srcW = m_cursorSize.cx;
    int srcH = m_cursorSize.cy;
    if (srcW <= 0 || srcH <= 0)
    {
        srcW = GetSystemMetrics(SM_CXCURSOR);
        srcH = GetSystemMetrics(SM_CYCURSOR);
    }

    const int dstW = (std::max)(1, static_cast<int>(std::lround(srcW * scale)));
    const int dstH = (std::max)(1, static_cast<int>(std::lround(srcH * scale)));

    EnsureResources(dstW, dstH);
    if (!m_memDc || !m_bits)
    {
        return;
    }

    std::memset(m_bits, 0, static_cast<size_t>(dstW) * static_cast<size_t>(dstH) * 4);
    if (cursorToDraw)
    {
        DrawIconEx(m_memDc, 0, 0, cursorToDraw, dstW, dstH, 0, nullptr, DI_NORMAL);
    }

    const int x = static_cast<int>(std::lround(ci.ptScreenPos.x - m_hotspot.x * scale));
    const int y = static_cast<int>(std::lround(ci.ptScreenPos.y - m_hotspot.y * scale));
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

void CursorMagnifierOverlay::BeginScaleAnimation()
{
    m_startScale = (m_targetScale >= 1.0f) ? 1.0f : m_targetScale;
    m_currentScale = m_startScale;
    m_animationStartTick = GetTickCount64();
}

float CursorMagnifierOverlay::GetAnimatedScale()
{
    if (m_animationDurationMs <= 1 || !m_visible)
    {
        m_currentScale = m_targetScale;
        return m_currentScale;
    }

    if (m_animationStartTick == 0)
    {
        m_animationStartTick = GetTickCount64();
    }

    const ULONGLONG now = GetTickCount64();
    const ULONGLONG elapsed = now - m_animationStartTick;
    if (elapsed >= m_animationDurationMs)
    {
        m_currentScale = m_targetScale;
        return m_currentScale;
    }

    const float t = static_cast<float>(elapsed) / static_cast<float>(m_animationDurationMs);
    m_currentScale = m_startScale + (m_targetScale - m_startScale) * t;
    return m_currentScale;
}

bool CursorMagnifierOverlay::HideSystemCursors()
{
    if (m_systemCursorsHidden)
    {
        return true;
    }

    m_hiddenCursorIds.clear();
    ReleaseOriginalCursors();

    bool anyHidden = false;
    bool normalHidden = false;
    for (UINT cursorId : kCursorIds)
    {
        HCURSOR systemCursor = LoadCursor(nullptr, MAKEINTRESOURCE(cursorId));
        if (systemCursor)
        {
            HCURSOR copy = CopyIcon(systemCursor);
            if (copy)
            {
                m_originalCursors[cursorId] = copy;
            }
        }

        HCURSOR transparent = CreateTransparentCursor();
        if (!transparent)
        {
            Logger::warn("CreateTransparentCursor failed for cursor id {}. GetLastError={}", cursorId, GetLastError());
            continue;
        }

        if (!SetSystemCursor(transparent, cursorId))
        {
            Logger::warn("SetSystemCursor failed for cursor id {}. GetLastError={}", cursorId, GetLastError());
            DestroyCursor(transparent);
            continue;
        }

        DestroyCursor(transparent);

        HCURSOR replacedCursor = LoadCursor(nullptr, MAKEINTRESOURCE(cursorId));
        if (replacedCursor)
        {
            m_hiddenCursorIds[replacedCursor] = cursorId;
        }

        anyHidden = true;
        if (cursorId == kCursorIdNormal)
        {
            normalHidden = true;
        }
    }

    if (!anyHidden)
    {
        m_hiddenCursorIds.clear();
        ReleaseOriginalCursors();
        return false;
    }

    if (!normalHidden)
    {
        Logger::warn("Failed to hide OCR_NORMAL; cursor may remain visible during magnifier.");
    }

    m_systemCursorsHidden = true;
    return true;
}

void CursorMagnifierOverlay::RestoreSystemCursors()
{
    if (!m_systemCursorsHidden)
    {
        return;
    }

    SystemParametersInfoW(SPI_SETCURSORS, 0, nullptr, 0);
    m_systemCursorsHidden = false;
    m_hiddenCursorIds.clear();
    ReleaseOriginalCursors();
}

HCURSOR CursorMagnifierOverlay::CreateTransparentCursor() const
{
    const int width = GetSystemMetrics(SM_CXCURSOR);
    const int height = GetSystemMetrics(SM_CYCURSOR);
    if (width <= 0 || height <= 0)
    {
        return nullptr;
    }

    const int bytesPerRow = (width + 7) / 8;
    const size_t maskSize = static_cast<size_t>(bytesPerRow) * static_cast<size_t>(height);
    std::vector<BYTE> andMask(maskSize, 0xFF);
    std::vector<BYTE> xorMask(maskSize, 0x00);

    return CreateCursor(m_instance, 0, 0, width, height, andMask.data(), xorMask.data());
}

void CursorMagnifierOverlay::ReleaseOriginalCursors()
{
    for (auto& entry : m_originalCursors)
    {
        if (entry.second)
        {
            DestroyIcon(entry.second);
        }
    }
    m_originalCursors.clear();
}

void CursorMagnifierOverlay::UpdateCursorMetrics(HCURSOR cursor)
{
    if (!cursor || cursor == m_cachedCursor)
    {
        return;
    }

    m_cursorSize = { 0, 0 };
    m_hotspot = { 0, 0 };

    ICONINFO ii{};
    if (GetIconInfo(cursor, &ii))
    {
        if (ii.hbmColor)
        {
            BITMAP bm{};
            GetObject(ii.hbmColor, sizeof(bm), &bm);
            m_cursorSize = { bm.bmWidth, bm.bmHeight };
        }
        else if (ii.hbmMask)
        {
            BITMAP bm{};
            GetObject(ii.hbmMask, sizeof(bm), &bm);
            m_cursorSize = { bm.bmWidth, bm.bmHeight / 2 };
        }

        m_hotspot = { static_cast<LONG>(ii.xHotspot), static_cast<LONG>(ii.yHotspot) };

        if (ii.hbmColor)
        {
            DeleteObject(ii.hbmColor);
        }
        if (ii.hbmMask)
        {
            DeleteObject(ii.hbmMask);
        }
    }

    m_cachedCursor = cursor;
    if (m_cursorSize.cx <= 0 || m_cursorSize.cy <= 0)
    {
        m_cursorSize = { GetSystemMetrics(SM_CXCURSOR), GetSystemMetrics(SM_CYCURSOR) };
    }
}

void CursorMagnifierOverlay::ResetCursorMetrics()
{
    m_cachedCursor = nullptr;
    m_cursorSize = { 0, 0 };
    m_hotspot = { 0, 0 };
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
    ResetCursorMetrics();
    RestoreSystemCursors();
}
