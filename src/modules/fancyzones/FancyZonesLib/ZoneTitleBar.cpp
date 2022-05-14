#include "pch.h"
#include "ZoneTitleBar.h"
#include "Drawing.h"
#include "CompositionDrawing.h"
#include <common/Themes/windows_colors.h>
#include <set>
#include <windowsx.h>

/* void Cls_OnDwmNcRenderingChanged(HWND hwnd, BOOL fEnabled) */
#define HANDLE_WM_DWMNCRENDERINGCHANGED(hwnd, wParam, lParam, fn) \
    ((fn)((hwnd), (BOOL)(wParam)), 0L)

/* void Cls_OnDwmColorizationColorChanged(HWND hwnd, DWORD dwNewColorizationColor, BOOL fIsBlendedWithOpacity) */
#define HANDLE_WM_DWMCOLORIZATIONCOLORCHANGED(hwnd, wParam, lParam, fn) \
    ((fn)((hwnd), (DWORD)(wParam), (BOOL)(lParam)), 0L)


using namespace FancyZonesUtils;

class ZoneTitleBarColors
{
public:
    ZoneTitleBarColors() :
        ZoneTitleBarColors(WindowsColors::is_dark_mode())
    {
    }

    ZoneTitleBarColors(bool isDarkMode)
    {
        backColor = Drawing::ConvertColor(WindowsColors::get_background_color());

        frameColor = Drawing::ConvertColor(isDarkMode ? WindowsColors::get_gray_text_color() : WindowsColors::get_button_face_color());
        textColor = Drawing::ConvertColor(WindowsColors::get_button_text_color());

        highlightFrameColor = Drawing::ConvertColor(WindowsColors::get_accent_color());
        highlightTextColor = Drawing::ConvertColor(WindowsColors::get_highlight_text_color());
    }

    D2D1_COLOR_F backColor;
    D2D1_COLOR_F frameColor;
    D2D1_COLOR_F textColor;
    D2D1_COLOR_F highlightFrameColor;
    D2D1_COLOR_F highlightTextColor;
};

class HiddenWindow : public Window
{
public:
    HiddenWindow(HINSTANCE hinstance)
    {
        Init(hinstance, nullptr, 0, WS_EX_TOOLWINDOW, Rect(0, 0, 0, 0), 0, 0, 0, SW_HIDE);
    }

    void HideWindowFromTaskbar(HWND window)
    {
        HWND val = *this;
        SetWindowLongPtr(window, GWLP_HWNDPARENT, (LONG_PTR)val);
    }
};

static HWND GetWindowAboveAllOthers(const std::vector<HWND>& windows)
{
    if (windows.empty())
    {
        return NULL;
    }

    std::set<HWND> windowsSet(windows.begin(), windows.end());

    // Get the window above all others
    HWND max = NULL;
    for (HWND current = windows.front(); !windows.empty() && current != NULL; current = GetWindow(current, GW_HWNDPREV))
    {
        auto i = windowsSet.find(current);
        if (i != windowsSet.end())
        {
            max = current;
            windowsSet.erase(i);
        }
    }

    return max;
}

static void SwitchToWindow(const std::vector<HWND>& windows, int i)
{
    if (i >= windows.size())
    {
        SwitchToWindow(windows.back());
    }
    else if (i <= 0)
    {
        SwitchToWindow(windows.front());
    }
    else
    {
        SwitchToWindow(windows[i]);
    }
}

static void DrawWindowIcon(Drawing& drawing, const D2D1_RECT_F& rect, HWND window, float opacity = 1.f)
{
    HICON icon = nullptr;
    if (!SendMessageTimeout(window, WM_GETICON, ICON_BIG, 0, 0, 100, (PDWORD_PTR)&icon) || icon == nullptr)
    {
        icon = (HICON)GetClassLongPtrW(window, GCLP_HICON);
    }

    if (icon != nullptr)
    {
        auto bitmap = drawing.CreateIcon(icon);
        drawing.DrawBitmap(rect, bitmap.get(), opacity);
    }
}

class NoZoneTitleBar : public IZoneTitleBar
{
public:
    NoZoneTitleBar(Rect zone) noexcept :
        m_zone(zone)
    {
    }

    void Show(bool show) override {}

    void UpdateZoneWindows(std::vector<HWND> zoneWindows) override {}

    void ReadjustPos() override {}

    Rect GetInlineFrame() const override { return m_zone; }

private:
    Rect m_zone;
};

class VisibleZoneTitleBar : public IZoneTitleBar
{
protected:
    VisibleZoneTitleBar(bool isAboveZone, Rect zone, UINT dpi) :
        m_isAboveZone(isAboveZone),
        m_zone(zone),
        m_dpi(dpi),
        m_zoneCurrentWindow(NULL)
    {
    }

    void Init(HINSTANCE hinstance, Rect rect, DWORD style, DWORD extendedStyle)
    {
        auto proc = [this](HWND window, UINT message, WPARAM wParam, LPARAM lParam) { return WndProc(window, message, wParam, lParam); };

        m_window.Init(hinstance, proc, style, extendedStyle, rect);
    }

    HWND GetDestinedWindowBeforeTheZoneTitleBar()
    {
        if (m_isAboveZone)
        {
            // Put the zone title bar just above the zone current window
            {
                // Get the window above the zone current window
                HWND windowAboveZoneCurrentWindow = GetWindow(m_zoneCurrentWindow, GW_HWNDPREV);

                // Put the zone title bar just below the windowAboveZoneCurrentWindow
                return windowAboveZoneCurrentWindow;
            }
        }
        else
        {
            // Put the zone title bar just below the zoneCurrentWindow
            return m_zoneCurrentWindow;
        }
    }

    void ReadjustPos() override
    {
        m_zoneCurrentWindow = GetWindowAboveAllOthers(m_zoneWindows);

        if (m_zoneCurrentWindow != NULL)
        {
            HWND windowBeforeTheZoneTitleBar = GetDestinedWindowBeforeTheZoneTitleBar();
            SetWindowPos(m_window, windowBeforeTheZoneTitleBar, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOOWNERZORDER | SWP_NOSIZE);
        }

        OnPaint(m_window);
    }

    LRESULT WndProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) noexcept
    {
        LRESULT result;
        auto handled = DwmDefWindowProc(window, message, wParam, lParam, &result);
        if (handled)
        {
            return result;
        }

        switch (message)
        {
            HANDLE_MSG(window, WM_NCCALCSIZE, OnCalcNonClientSize);
            HANDLE_MSG(window, WM_CREATE, OnCreate);
            HANDLE_MSG(window, WM_WINDOWPOSCHANGING, WindowPosChanging);
            HANDLE_MSG(window, WM_DWMNCRENDERINGCHANGED, DwmNonClientRenderingChanged);
            HANDLE_MSG(window, WM_DWMCOLORIZATIONCOLORCHANGED, OnDwmColorizationColorChanged);
            HANDLE_MSG(window, WM_PAINT, OnPaint);
            HANDLE_MSG(window, WM_LBUTTONDOWN, OnLButtonDown);
            HANDLE_MSG(window, WM_ERASEBKGND, OnEraseBackground);

        default:
            return DefWindowProcW(window, message, wParam, lParam);
        }
    }

    UINT OnCalcNonClientSize(HWND hwnd, BOOL calc, NCCALCSIZE_PARAMS* info)
    {
        if (!calc)
        {
            return FORWARD_WM_NCCALCSIZE(hwnd, calc, info, DefWindowProcW);
        }

        if (GetWindowLong(hwnd, GWL_STYLE) & WS_CAPTION)
        {
            auto xBorder = GetSystemMetricsForDpi(SM_CXFRAME, m_dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, m_dpi);
            auto yBorder = GetSystemMetricsForDpi(SM_CYFRAME, m_dpi) + GetSystemMetricsForDpi(SM_CYEDGE, m_dpi);

            auto& coordinates = info->rgrc[0];
            coordinates.left += xBorder;
            coordinates.right -= xBorder;
            coordinates.bottom -= yBorder;
        }

        return 0;
    }

    virtual bool OnCreate(HWND hwnd, LPCREATESTRUCT)
    {
        // Disable transitions
        BOOL disable = TRUE;
        DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, &disable, sizeof(disable));

        return true;
    }

    void OnDwmColorizationColorChanged(HWND hwnd, DWORD newColorizationColor, BOOL isBlendedWithOpacity)
    {
        // Post WM_PAINT
        RedrawWindow(hwnd, NULL, NULL, RDW_INTERNALPAINT);
    }

    void DwmNonClientRenderingChanged(HWND hwnd, BOOL enabled)
    {
        Rect newWindowRect = m_zone;

        RECT windowRect{};
        ::GetWindowRect(hwnd, &windowRect);

        // Take care of borders
        RECT frameRect{};
        if (SUCCEEDED(DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &frameRect, sizeof(frameRect))))
        {
            LONG leftMargin = frameRect.left - windowRect.left;
            LONG rightMargin = frameRect.right - windowRect.right;
            LONG bottomMargin = frameRect.bottom - windowRect.bottom;

            newWindowRect.get()->left -= leftMargin;
            newWindowRect.get()->right -= rightMargin;
            newWindowRect.get()->bottom -= bottomMargin;

            SetWindowPos(hwnd, nullptr, newWindowRect.left(), newWindowRect.top(), newWindowRect.width(), newWindowRect.height(), SWP_NOACTIVATE | SWP_NOZORDER);
        }
    }

    virtual void OnLButtonDown(HWND hwnd, BOOL doubleClick, int x, int y, UINT keyFlags) = 0;

    virtual void OnPaint(HWND hwnd) = 0;

    BOOL OnEraseBackground(HWND hwnd, HDC hdc)
    {
        return TRUE;
    }

    void UpdateZoneWindows(std::vector<HWND> zoneWindows) override
    {
        m_zoneWindows = zoneWindows;
        ReadjustPos();
    }

    BOOL WindowPosChanging(HWND hwnd, WINDOWPOS* pos)
    {
        if ((pos->flags & SWP_NOZORDER) == 0)
        {
            pos->hwndInsertAfter = GetDestinedWindowBeforeTheZoneTitleBar();
        }

        pos->flags |= SWP_NOACTIVATE;

        return true;
    }

    int GetScale() const
    {
        auto scale = GetSystemMetricsForDpi(SM_CYHSCROLL, m_dpi);
        if (scale > m_zone.height())
        {
            scale = 0;
        }

        return scale;
    }

    bool m_isAboveZone;
    UINT m_dpi;
    Rect m_zone;
    std::vector<HWND> m_zoneWindows;
    HWND m_zoneCurrentWindow;
    Window m_window;

public:
    void Show(bool show) override {}
};

class SlimZoneTitleBar : public VisibleZoneTitleBar
{
public:
    SlimZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, bool isAboveZone) noexcept :
        VisibleZoneTitleBar(isAboveZone, zone, dpi),
        m_height(GetScale())
    {
        Rect rect(zone.position(), zone.width(), m_height);
        Init(hinstance, rect, WS_POPUP, WS_EX_TOOLWINDOW);
    }

    Rect GetInlineFrame() const override
    {
        auto rect = m_zone;
        rect.get()->top += m_height;
        return rect;
    }

protected:
    bool OnCreate(HWND hwnd, LPCREATESTRUCT)
    {
        m_drawing.Init(hwnd);

        return true;
    }

    void OnLButtonDown(HWND hwnd, BOOL doubleClick, int x, int y, UINT keyFlags)
    {
        auto len = m_height;
        if (len == 0)
        {
            return;
        }

        auto i = x / len;

        if (i >= 0 && i < m_zoneWindows.size())
        {
            SwitchToWindow(m_zoneWindows[i]);
        }
    }

    void OnPaint(HWND hwnd) override
    {
        ZoneTitleBarColors colors;

        PAINTSTRUCT paint;
        BeginPaint(m_window, &paint);

        if (m_drawing)
        {
            m_drawing.BeginDraw(colors.backColor);

            Render(colors);

            m_drawing.EndDraw();
        }

        EndPaint(m_window, &paint);
    }

    virtual void Render(ZoneTitleBarColors& colors) = 0;

protected:
    int m_height;
    Drawing m_drawing;
};

class NumbersZoneTitleBar : public SlimZoneTitleBar
{
public:
    NumbersZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, bool isAboveZone) noexcept :
        SlimZoneTitleBar(hinstance, zone, dpi, isAboveZone)
    {
    }

    void Render(ZoneTitleBarColors& colors) override
    {
        auto len = (FLOAT)m_height;

        auto textFormat = m_drawing.CreateTextFormat(L"Segoe ui", len * .7f);
        for (auto i = 0; i < m_zoneWindows.size(); ++i)
        {
            auto rect = D2D1::Rect(len * i, .0f, len * (i + 1), len);

            if (m_zoneWindows[i] == m_zoneCurrentWindow)
            {
                m_drawing.FillRectangle(rect, colors.highlightFrameColor);
                m_drawing.DrawTextW(std::to_wstring(i + 1), textFormat.get(), rect, colors.highlightTextColor);
            }
            else
            {
                m_drawing.FillRectangle(rect, colors.frameColor);
                m_drawing.DrawTextW(std::to_wstring(i + 1), textFormat.get(), rect, colors.textColor);
            }
        }
    }
};

class IconsZoneTitleBar : public SlimZoneTitleBar
{
public:
    IconsZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, bool isAboveZone) noexcept :
        SlimZoneTitleBar(hinstance, zone, dpi, isAboveZone)
    {
    }

    void Render(ZoneTitleBarColors& colors) override
    {
        auto len = (FLOAT)m_height;

        for (auto i = 0; i < m_zoneWindows.size(); ++i)
        {
            constexpr float p = .15f;
            auto iconRect = D2D1::Rect(len * (i + p), len * p, len * (i + 1 - p), len * (1 - p));
            DrawWindowIcon(m_drawing, iconRect, m_zoneWindows[i]);

            constexpr float s = p * .7f;
            auto strokeRect = D2D1::Rect(len * (i + .5f * s), len * .5f * s, len * (i + 1 - .5f * s), len * (1 - .5f * s));
            auto color = m_zoneWindows[i] == m_zoneCurrentWindow ? colors.highlightFrameColor : colors.frameColor;
            m_drawing.DrawRoundedRectangle(strokeRect, color, len * s);
        }
    }
};

class ThickZoneTitleBar : public VisibleZoneTitleBar
{
protected:
    static constexpr int c_style = WS_OVERLAPPED | WS_CAPTION | WS_THICKFRAME;
    static constexpr int c_exStyle = WS_EX_NOREDIRECTIONBITMAP;

    float GetWidthFactor() const
    {
        constexpr int c_widthFactor = 4;

        auto left = float(m_zone.width() - 2 * m_height);
        auto amount = m_zoneWindows.size();

        if (amount == 0 || left <= 0)
        {
            return 0;
        }

        auto factor = (left / amount) / m_height;
        return min(factor, c_widthFactor);
    }

public:
    ThickZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, bool isAboveZone) noexcept :
        VisibleZoneTitleBar(isAboveZone, zone, dpi),
        m_hiddenWindow(hinstance)
    {
        RECT rect{};
        AdjustWindowRectExForDpi(&rect, c_style, FALSE, c_exStyle, m_dpi);

        auto height = -rect.top;
        m_height = height > zone.height() ? 0 : height;

        Init(hinstance, zone, c_style, c_exStyle);
    }

    Rect GetInlineFrame() const override
    {
        auto rect = m_zone;
        rect.get()->top += m_height;
        return rect;
    }

protected:
    bool OnCreate(HWND hwnd, LPCREATESTRUCT createStruct)
    {
        VisibleZoneTitleBar::OnCreate(hwnd, createStruct);

        // Hide from taskbar
        m_hiddenWindow.HideWindowFromTaskbar(hwnd);

        // Extend frame (twice the size if not overlay)
        int height = m_isAboveZone ? m_height : 2 * m_height;
        MARGINS margins = { 0, 0, height, 0 };
        DwmExtendFrameIntoClientArea(hwnd, &margins);

        // Update frame
        SetWindowPos(hwnd, nullptr, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);

        // Initialize drawing
        m_drawing.Init(hwnd);

        return true;
    }

    void OnLButtonDown(HWND hwnd, BOOL doubleClick, int x, int y, UINT keyFlags)
    {
        auto len = m_height;
        if (len == 0 || x < len)
        {
            return;
        }

        auto i = int((x - len) / (len * GetWidthFactor()));

        if (i >= 0 && i < m_zoneWindows.size())
        {
            SwitchToWindow(m_zoneWindows[i]);
        }
    }

protected:
    HiddenWindow m_hiddenWindow;
    int m_height;
    CompositionDrawing m_drawing;
};

class TabsZoneTitleBar : public ThickZoneTitleBar
{
public:
    TabsZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, bool isAboveZone) noexcept :
        ThickZoneTitleBar(hinstance, zone, dpi, isAboveZone)
    {
    }

protected:
    void OnPaint(HWND hwnd) override
    {
        constexpr DWORD DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        BOOL isDarkMode = WindowsColors::is_dark_mode();
        if (FAILED(DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &isDarkMode, sizeof(isDarkMode))))
        {
            isDarkMode = false;
        }

        ZoneTitleBarColors colors(isDarkMode);

        PAINTSTRUCT paint;
        BeginPaint(m_window, &paint);

        auto captionHeight = GetSystemMetricsForDpi(SM_CYCAPTION, m_dpi);

        auto zoneCurrentWindow = m_zoneCurrentWindow;

        NONCLIENTMETRICS metrics{};
        metrics.cbSize = sizeof(metrics);
        SystemParametersInfoForDpi(SPI_GETNONCLIENTMETRICS, sizeof(NONCLIENTMETRICS), &metrics, 0, m_dpi);
        TCHAR text[100]{};

        if (m_drawing)
        {
            m_drawing.BeginDraw();

            {
                auto widthFactor = GetWidthFactor();
                auto textFormat = m_drawing.CreateTextFormat(
                    metrics.lfCaptionFont.lfFaceName,
                    float(-metrics.lfCaptionFont.lfHeight),
                    (DWRITE_FONT_WEIGHT)metrics.lfCaptionFont.lfWeight);

                for (auto i = 0; i < m_zoneWindows.size(); ++i)
                {
                    auto xOffset = m_height * (1 + widthFactor * i);
                    auto yOffset = 0;

                    auto backMargin = (m_height - captionHeight) * .4f;
                    auto backHeight = m_height - 2 * backMargin;
                    auto backWidth = widthFactor * m_height - 2 * backMargin;
                    auto backRect = D2D1::RectF(
                        float(xOffset + backMargin),
                        float(yOffset + backMargin),
                        float(xOffset + backMargin + backWidth),
                        float(yOffset + backMargin + backHeight));
                    m_drawing.FillRoundedRectangle(backRect, m_zoneWindows[i] == zoneCurrentWindow ? colors.highlightFrameColor : colors.frameColor);

                    auto iconMargin = (m_height - captionHeight) * .5f;
                    auto iconRect = D2D1::Rect(
                        xOffset + iconMargin,
                        yOffset + iconMargin,
                        xOffset + iconMargin + captionHeight,
                        yOffset + iconMargin + captionHeight);
                    DrawWindowIcon(m_drawing, iconRect, m_zoneWindows[i]);

                    if (textFormat)
                    {
                        auto textMargin = (m_height - captionHeight) * .5f;
                        auto textRect = D2D1::Rect(
                            float(xOffset + m_height),
                            float(yOffset + textMargin),
                            float(xOffset + widthFactor * m_height - textMargin),
                            float(yOffset + iconMargin + captionHeight));

                        text[0] = TEXT('\0');
                        GetWindowText(m_zoneWindows[i], text, ARRAYSIZE(text));
                        m_drawing.DrawTextTrim(text, textFormat.get(), textRect, m_zoneWindows[i] == zoneCurrentWindow ? colors.highlightTextColor : colors.textColor);
                    }
                }
            }

            m_drawing.EndDraw();
        }

        EndPaint(m_window, &paint);
    }
};

class LabelsZoneTitleBar : public ThickZoneTitleBar
{
public:
    LabelsZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, bool isAboveZone) noexcept :
        ThickZoneTitleBar(hinstance, zone, dpi, isAboveZone)
    {
    }

protected:
    void OnPaint(HWND hwnd) override
    {
        constexpr DWORD DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        BOOL isDarkMode = WindowsColors::is_dark_mode();
        if (FAILED(DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &isDarkMode, sizeof(isDarkMode))))
        {
            isDarkMode = false;
        }

        ZoneTitleBarColors colors(isDarkMode);

        PAINTSTRUCT paint;
        BeginPaint(m_window, &paint);

        auto captionHeight = GetSystemMetricsForDpi(SM_CYCAPTION, m_dpi);

        auto zoneCurrentWindow = m_zoneCurrentWindow;

        NONCLIENTMETRICS metrics{};
        metrics.cbSize = sizeof(metrics);
        SystemParametersInfoForDpi(SPI_GETNONCLIENTMETRICS, sizeof(NONCLIENTMETRICS), &metrics, 0, m_dpi);
        TCHAR text[100]{};

        if (m_drawing)
        {
            m_drawing.BeginDraw();

            {
                auto widthFactor = GetWidthFactor();
                auto textFormat = m_drawing.CreateTextFormat(
                    metrics.lfCaptionFont.lfFaceName,
                    float(-metrics.lfCaptionFont.lfHeight),
                    (DWRITE_FONT_WEIGHT)metrics.lfCaptionFont.lfWeight);

                auto textColor = isDarkMode ? colors.frameColor : Drawing::ConvertColor(WindowsColors::get_gray_text_color());
                auto highlightTextColor = isDarkMode ? colors.highlightTextColor : colors.textColor;

                for (auto i = 0; i < m_zoneWindows.size(); ++i)
                {
                    auto xOffset = m_height * (1 + widthFactor * i);
                    auto yOffset = 0;
                    auto xSize = m_height * widthFactor;
                    auto ySize = m_height;

                    auto sepHeight = captionHeight * .7f;
                    auto sepWidth = sepHeight * .05f;
                    auto sepMargin = (ySize - sepHeight) * .5f;
                    if (i != 0)
                    {
                        auto sepRect = D2D1::RectF(
                            float(xOffset - sepWidth * .5f),
                            float(yOffset + sepMargin),
                            float(xOffset + sepWidth * .5f),
                            float(yOffset + ySize - sepMargin));
                        m_drawing.FillRectangle(sepRect, textColor);
                    }

                    auto isFrontWindow = m_zoneWindows[i] == zoneCurrentWindow;
                    auto xMargin = sepHeight * .9f;

                    auto iconMargin = (ySize - captionHeight) * .5f;
                    auto iconRect = D2D1::RectF(
                        xOffset + xMargin,
                        yOffset + iconMargin,
                        xOffset + xMargin + captionHeight,
                        yOffset + iconMargin + captionHeight);
                    DrawWindowIcon(m_drawing, iconRect, m_zoneWindows[i], isFrontWindow ? 1.f : .4f);

                    auto xSizeIcon = 0;

                    if (textFormat)
                    {
                        auto textMargin = (ySize - captionHeight) * .5f;
                        auto textRect = D2D1::RectF(
                            float(xOffset + xMargin + captionHeight * 1.1f),
                            float(yOffset + textMargin),
                            float(xOffset + xSize - xMargin),
                            float(yOffset + ySize - textMargin));

                        text[0] = TEXT('\0');
                        GetWindowText(m_zoneWindows[i], text, ARRAYSIZE(text));
                        m_drawing.DrawTextTrim(text, textFormat.get(), textRect, isFrontWindow ? highlightTextColor : textColor, isFrontWindow);
                    }
                }
            }

            m_drawing.EndDraw();
        }

        EndPaint(m_window, &paint);
    }
};

class AdornZoneTitleBar : public VisibleZoneTitleBar
{
protected:
    static constexpr int c_style = WS_POPUP;
    static constexpr int c_exStyle = WS_EX_TOOLWINDOW | WS_EX_LAYERED;

public:
    AdornZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi) noexcept :
        VisibleZoneTitleBar(true, zone, dpi)
    {
        Init(hinstance, zone, c_style, c_exStyle);
    }

    Rect GetInlineFrame() const override
    {
        return m_zone;
    }

protected:
    bool OnCreate(HWND hwnd, LPCREATESTRUCT createStruct) override
    {
        VisibleZoneTitleBar::OnCreate(hwnd, createStruct);

        // Initialize drawing
        m_drawing.Init(hwnd);

        // Set layered window
        SetLayeredWindowAttributes(hwnd, RGB(0, 0, 0), 0, LWA_COLORKEY);

        return true;
    }

protected:
    Drawing m_drawing;
};

class PagerZoneTitleBar : public AdornZoneTitleBar
{
public:
    PagerZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi) noexcept :
        AdornZoneTitleBar(hinstance, zone, dpi)
    {
    }

protected:
    void OnLButtonDown(HWND hwnd, BOOL doubleClick, int x, int y, UINT keyFlags) override
    {
        auto q = (FLOAT)GetSystemMetricsForDpi(SM_CYHSCROLL, m_dpi);
        if (q == 0 || m_zoneWindows.size() == 0)
        {
            return;
        }

        auto h = m_zone.height();
        auto w = m_zone.width();

        auto m = q * .25f;
        auto l = h - (q + m);

        if (y < l)
        {
            auto ptr = std::find(m_zoneWindows.begin(), m_zoneWindows.end(), m_zoneCurrentWindow);
            if (ptr == m_zoneWindows.end())
            {
                return;
            }

            auto i = std::distance(m_zoneWindows.begin(), ptr);

            auto last = m_zoneWindows.size() - 1;
            if (x < w / 2)
            {
                auto prev = (i == 0) ? last : (i - 1);
                SwitchToWindow(m_zoneWindows[prev]);
            }
            else
            {
                auto next = (i == last) ? 0 : (i + 1);
                SwitchToWindow(m_zoneWindows[next]);
            }
        }
        else
        {
            auto t = m_zoneWindows.size() * q;
            auto o = (w - t) * .5f;

            auto i = (int)((x - o) / q);
            SwitchToWindow(m_zoneWindows, i);
        }
    }

    void OnPaint(HWND hwnd) override
    {
        PAINTSTRUCT paint;
        BeginPaint(m_window, &paint);

        if (m_drawing)
        {
            m_drawing.Drawing::BeginDraw(D2D1::ColorF(0.f, 0.f, 0.f, 0.f));

            auto color = Drawing::ConvertColor(WindowsColors::get_gray_text_color());

            auto q = (FLOAT)GetSystemMetricsForDpi(SM_CYHSCROLL, m_dpi);

            auto h = m_zone.height();
            auto w = m_zone.width();

            auto m = q * .25f;
            auto l = h - (q + m);

            auto y = q * .7f;
            auto z = q * .3f;
            {
                D2D1_TRIANGLE triangle = {
                    .point1 = D2D1::Point2F(m, l / 2),
                    .point2 = D2D1::Point2F(m + y * .86602540378f, (l / 2) - (y / 2)),
                    .point3 = D2D1::Point2F(m + y * .86602540378f, (l / 2) + (y / 2))
                };

                auto geometry = m_drawing.CreateTriangle(triangle);

                if (geometry)
                {
                    m_drawing.FillGeometry(geometry.get(), color, z * .5f);
                }
            }

            {
                D2D1_TRIANGLE triangle = {
                    .point1 = D2D1::Point2F(w - m, l / 2),
                    .point2 = D2D1::Point2F(w - (m + y * .86602540378f), (l / 2) - (y / 2)),
                    .point3 = D2D1::Point2F(w - (m + y * .86602540378f), (l / 2) + (y / 2))
                };

                auto geometry = m_drawing.CreateTriangle(triangle);

                if (geometry)
                {
                    m_drawing.FillGeometry(geometry.get(), color, z * .5f);
                }
            }

            auto zoneCurrentWindow = m_zoneCurrentWindow;
            auto t = m_zoneWindows.size() * q;
            auto o = (w - t) * .5f;
            for (auto i = 0; i < m_zoneWindows.size(); ++i)
            {
                auto center = D2D1::Point2(o + q * (i + .5f), l + (q * .5f));
                auto radius = q * .25f * (m_zoneWindows[i] == zoneCurrentWindow ? 2.f : 1.f);
                auto circle = D2D1::Ellipse(center, radius, radius);
                m_drawing.FillEllipse(circle, color);
            }

            m_drawing.EndDraw();
        }

        EndPaint(m_window, &paint);
    }
};

class SideZoneTitleBar : public VisibleZoneTitleBar
{
protected:
    static constexpr int c_style = WS_POPUP;
    static constexpr int c_exStyle = WS_EX_TOOLWINDOW | WS_EX_LAYERED;

    int GetWidthFactor() const
    {
        constexpr int c_widthFactor = 2;

        auto q = c_widthFactor * GetSystemMetricsForDpi(SM_CYHSCROLL, m_dpi);

        if (q < 0 || m_zone.width() < 2 * q)
        {
            return 0;
        }

        return q;
    }

public:
    SideZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, bool isAboveZone) noexcept :
        VisibleZoneTitleBar(isAboveZone, zone, dpi)
    {
        auto q = GetWidthFactor();
        zone.get()->right = zone.left() + q;

        Init(hinstance, zone, c_style, c_exStyle);
    }

    Rect GetInlineFrame() const override
    {
        auto q = GetWidthFactor();

        Rect zone = m_zone;
        zone.get()->left += q;

        return zone;
    }

protected:
    bool OnCreate(HWND hwnd, LPCREATESTRUCT createStruct) override
    {
        VisibleZoneTitleBar::OnCreate(hwnd, createStruct);

        // Initialize drawing
        m_drawing.Init(hwnd);

        // Set layered window
        SetLayeredWindowAttributes(hwnd, RGB(0, 0, 0), 0, LWA_COLORKEY);

        return true;
    }

protected:
    Drawing m_drawing;
};

class ButtonsZoneTitleBar : public SideZoneTitleBar
{
public:
    ButtonsZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, bool isAboveZone) noexcept :
        SideZoneTitleBar(hinstance, zone, dpi, isAboveZone)
    {
    }

protected:
    void OnLButtonDown(HWND hwnd, BOOL doubleClick, int x, int y, UINT keyFlags) override
    {
        auto q = GetWidthFactor();
        if (q <= 0)
        {
            return;
        }

        auto i = y / q;
        SwitchToWindow(m_zoneWindows, i);
    }

    void OnPaint(HWND hwnd) override
    {
        PAINTSTRUCT paint;
        BeginPaint(m_window, &paint);

        if (m_drawing)
        {
            ZoneTitleBarColors colors;
            auto q = GetWidthFactor();
            m_drawing.Drawing::BeginDraw(D2D1::ColorF(0.f, 0.f, 0.f, 0.f));

            for (auto i = 0; i < m_zoneWindows.size(); ++i)
            {
                auto m = .1f * q;
                auto rect = D2D1::RectF(0.f + m, i * q + m, q - m, (i + 1) * q - m);

                if (m_zoneWindows[i] == m_zoneCurrentWindow)
                {
                    m_drawing.FillRoundedRectangle(rect, colors.highlightFrameColor, .3f);
                }
                else
                {
                    m_drawing.FillRoundedRectangle(rect, colors.frameColor, .3f);
                }

                m = .17f * q;
                auto iconRect = D2D1::RectF(0.f + m, i * q + m, q - m, (i + 1) * q - m);
                DrawWindowIcon(m_drawing, iconRect, m_zoneWindows[i]);
            }

            m_drawing.EndDraw();
        }

        EndPaint(m_window, &paint);
    }
};

class AutoHideZoneTitleBar : public IZoneTitleBar
{
public:
    AutoHideZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi, ZoneTitleBarStyle style) :
        m_hinstance(hinstance),
        m_zone(zone),
        m_dpi(dpi),
        m_style(style)
    {
    }

    virtual void Show(bool show) override
    {
        if (show)
        {
            if (!m_zoneTitleBar)
            {
                switch (m_style)
                {
                case ZoneTitleBarStyle::Numbers:
                    m_zoneTitleBar = std::make_unique<NumbersZoneTitleBar>(m_hinstance, m_zone, m_dpi, true);
                    break;

                case ZoneTitleBarStyle::Icons:
                    m_zoneTitleBar = std::make_unique<IconsZoneTitleBar>(m_hinstance, m_zone, m_dpi, true);
                    break;

                case ZoneTitleBarStyle::Tabs:
                    m_zoneTitleBar = std::make_unique<TabsZoneTitleBar>(m_hinstance, m_zone, m_dpi, true);
                    break;

                case ZoneTitleBarStyle::Labels:
                    m_zoneTitleBar = std::make_unique<LabelsZoneTitleBar>(m_hinstance, m_zone, m_dpi, true);
                    break;

                case ZoneTitleBarStyle::Pager:
                    m_zoneTitleBar = std::make_unique<PagerZoneTitleBar>(m_hinstance, m_zone, m_dpi);
                    break;

                case ZoneTitleBarStyle::Buttons:
                    m_zoneTitleBar = std::make_unique<ButtonsZoneTitleBar>(m_hinstance, m_zone, m_dpi, true);
                    break;

                case ZoneTitleBarStyle::None:
                default:
                    m_zoneTitleBar = std::make_unique<NoZoneTitleBar>(m_zone);
                    break;
                }

                m_zoneTitleBar->UpdateZoneWindows(m_zoneWindows);
            }
        }
        else
        {
            m_zoneTitleBar.reset();
        }
    }

    virtual void UpdateZoneWindows(std::vector<HWND> zoneWindows) override
    {
        m_zoneWindows = zoneWindows;

        if (m_zoneTitleBar)
        {
            m_zoneTitleBar->UpdateZoneWindows(zoneWindows);
        }
    }

    virtual void ReadjustPos() override
    {
        if (m_zoneTitleBar)
        {
            m_zoneTitleBar->ReadjustPos();
        }
    }

    Rect GetInlineFrame() const override
    {
        return m_zone;
    }

protected:
    HINSTANCE m_hinstance;
    Rect m_zone;
    UINT m_dpi;
    ZoneTitleBarStyle m_style;
    std::vector<HWND> m_zoneWindows;

    std::unique_ptr<IZoneTitleBar> m_zoneTitleBar;
};

std::unique_ptr<IZoneTitleBar> MakeZoneTitleBar(ZoneTitleBarStyle style, HINSTANCE hinstance, Rect zone, UINT dpi)
{
    bool isAutoHide = ((int)style & (int)ZoneTitleBarStyle::AutoHide) && style != ZoneTitleBarStyle::AutoHide;
    if (isAutoHide)
    {
        return std::make_unique<AutoHideZoneTitleBar>(hinstance, zone, dpi, (ZoneTitleBarStyle)((int)style & ~(int)ZoneTitleBarStyle::AutoHide));
    }

    switch (style)
    {
    case ZoneTitleBarStyle::Numbers:
        return std::make_unique<NumbersZoneTitleBar>(hinstance, zone, dpi, false);

    case ZoneTitleBarStyle::Icons:
        return std::make_unique<IconsZoneTitleBar>(hinstance, zone, dpi, false);

    case ZoneTitleBarStyle::Tabs:
        return std::make_unique<TabsZoneTitleBar>(hinstance, zone, dpi, false);

    case ZoneTitleBarStyle::Labels:
        return std::make_unique<LabelsZoneTitleBar>(hinstance, zone, dpi, false);

    case ZoneTitleBarStyle::Pager:
        return std::make_unique<PagerZoneTitleBar>(hinstance, zone, dpi);

    case ZoneTitleBarStyle::Buttons:
        return std::make_unique<ButtonsZoneTitleBar>(hinstance, zone, dpi, false);

    case ZoneTitleBarStyle::None:
    default:
        return std::make_unique<NoZoneTitleBar>(zone);
    }
}
