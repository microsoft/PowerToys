#include "pch.h"
#include "ZoneTitleBar.h"
#include "Drawing.h"
#include "CompositionDrawing.h"
#include <set>
#include <windowsx.h>

/* void Cls_OnDwmNcRenderingChanged(HWND hwnd, BOOL fEnabled) */
#define HANDLE_WM_DWMNCRENDERINGCHANGED(hwnd, wParam, lParam, fn) \
    ((fn)((hwnd), (BOOL)(wParam)), 0L)


using namespace FancyZonesUtils;


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

static void DrawWindowIcon(Drawing& drawing, const D2D1_RECT_F& rect, HWND window)
{
    auto icon = (HICON)SendMessageW(window, WM_GETICON, ICON_BIG, 0);
    if (icon == nullptr)
    {
        icon = (HICON)GetClassLongPtrW(window, GCLP_HICON);
    }

    if (icon != nullptr)
    {
        auto bitmap = drawing.CreateIcon(icon);
        drawing.DrawBitmap(rect, bitmap.get());
    }
}

class NoZoneTitleBar : public IZoneTitleBar
{
public:
    NoZoneTitleBar() noexcept {}

    void UpdateZoneWindows(std::vector<HWND> zoneWindows) override {}

    void ReadjustPos() override {}

    int GetHeight() const override { return 0; }
};

class SlimZoneTitleBar : public IZoneTitleBar
{
public:
    SlimZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi) noexcept :
        m_dpi(dpi),
        m_window(
            hinstance,
            [this](HWND window, UINT message, WPARAM wParam, LPARAM lParam) { return WndProc(window, message, wParam, lParam); },
            WS_POPUP,
            WS_EX_TOOLWINDOW,
            ResetRect(zone),
            NULL,
            NULL,
            NULL)
    {
    }

    void UpdateZoneWindows(std::vector<HWND> zoneWindows) override
    {
        m_zoneWindows = zoneWindows;
        ReadjustPos();
    }

    void ReadjustPos() override
    {
        auto zoneCurrentWindow = GetZoneCurrentWindow();

        // Put the zone title bar just above the zone current window
        if (zoneCurrentWindow != NULL)
        {
            // Get the window above the zone current window
            HWND windowAboveZoneCurrentWindow = GetWindow(zoneCurrentWindow, GW_HWNDPREV);

            // Put the zone title bar just below the windowAboveZoneCurrentWindow
            SetWindowPos(m_window, windowAboveZoneCurrentWindow, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOOWNERZORDER | SWP_NOSIZE);
        }

        Render(m_window);
    }

    int GetHeight() const override { return m_height; }

    virtual void Render(HWND window) = 0;

protected:
    LRESULT WndProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) noexcept
    {
        switch (message)
        {
            HANDLE_MSG(window, WM_CREATE, Init);
            HANDLE_MSG(window, WM_PAINT, Render);
            HANDLE_MSG(window, WM_LBUTTONDOWN, Click);
        default:
            return DefWindowProcW(window, message, wParam, lParam);
        }
    }

    bool Init(HWND hwnd, LPCREATESTRUCT)
    {
        m_drawing.Init(hwnd);

        return true;
    }

    void Click(HWND hwnd, BOOL doubleClick, int x, int y, UINT keyFlags)
    {
        auto len = m_height;
        if (len == 0)
        {
            return;
        }

        auto i = x / len;

        if (i < m_zoneWindows.size())
        {
            SwitchToWindow(m_zoneWindows[i]);
        }
    }

    HWND GetZoneCurrentWindow()
    {
        return GetWindowAboveAllOthers(m_zoneWindows);
    }

    Rect ResetRect(Rect zone)
    {
        int height = GetSystemMetricsForDpi(SM_CYHSCROLL, m_dpi);
        m_height = height > zone.height() ? 0 : height;
        return Rect(zone.position(), zone.width(), m_height);
    }

protected:
    UINT m_dpi;
    int m_height;
    std::vector<HWND> m_zoneWindows;
    Drawing m_drawing;
    Window m_window;
};

class NumbersZoneTitleBar : public SlimZoneTitleBar
{
public:
    NumbersZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi) noexcept :
        SlimZoneTitleBar(hinstance, zone, dpi)
    {
    }

    void Render(HWND hwnd) override
    {
        PAINTSTRUCT paint;
        BeginPaint(m_window, &paint);

        auto backColor = Drawing::ConvertColor(GetSysColor(COLOR_WINDOW));
        auto frameColor = Drawing::ConvertColor(GetSysColor(COLOR_3DFACE));
        auto textColor = Drawing::ConvertColor(GetSysColor(COLOR_BTNTEXT));

        auto highlightFrameColor = Drawing::ConvertColor(GetSysColor(COLOR_HIGHLIGHT));
        auto highlightTextColor = Drawing::ConvertColor(GetSysColor(COLOR_HIGHLIGHTTEXT));

        auto zoneCurrentWindow = GetZoneCurrentWindow();

        if (m_drawing)
        {
            m_drawing.BeginDraw(backColor);

            {
                auto len = (FLOAT)m_height;

                auto textFormat = m_drawing.CreateTextFormat(L"Segoe ui", len * .7f);
                for (auto i = 0; i < m_zoneWindows.size(); ++i)
                {
                    auto rect = D2D1::Rect(len * i, .0f, len * (i + 1), len);

                    if (m_zoneWindows[i] == zoneCurrentWindow)
                    {
                        m_drawing.FillRectangle(rect, highlightFrameColor);
                        m_drawing.DrawTextW(std::to_wstring(i + 1), textFormat.get(), rect, highlightTextColor);
                    }
                    else
                    {
                        m_drawing.FillRectangle(rect, frameColor);
                        m_drawing.DrawTextW(std::to_wstring(i + 1), textFormat.get(), rect, textColor);
                    }
                }
            }

            m_drawing.EndDraw();
        }

        EndPaint(m_window, &paint);
    }
};

class IconsZoneTitleBar : public SlimZoneTitleBar
{
public:
    IconsZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi) noexcept :
        SlimZoneTitleBar(hinstance, zone, dpi)
    {
    }

    void Render(HWND hwnd) override
    {
        PAINTSTRUCT paint;
        BeginPaint(m_window, &paint);

        auto backColor = Drawing::ConvertColor(GetSysColor(COLOR_WINDOW));
        auto frameColor = Drawing::ConvertColor(GetSysColor(COLOR_3DFACE));
        auto textColor = Drawing::ConvertColor(GetSysColor(COLOR_BTNTEXT));

        auto highlightFrameColor = Drawing::ConvertColor(GetSysColor(COLOR_HIGHLIGHT));
        auto highlightTextColor = Drawing::ConvertColor(GetSysColor(COLOR_HIGHLIGHTTEXT));

        auto zoneCurrentWindow = GetZoneCurrentWindow();

        if (m_drawing)
        {
            m_drawing.BeginDraw(backColor);

            {
                auto len = (FLOAT)m_height;

                for (auto i = 0; i < m_zoneWindows.size(); ++i)
                {
                    constexpr float p = .15f;
                    auto iconRect = D2D1::Rect(len * (i + p), len * p, len * (i + 1 - p), len * (1 - p));
                    DrawWindowIcon(m_drawing, iconRect, m_zoneWindows[i]);

                    constexpr float s = p * .7f;
                    auto strokeRect = D2D1::Rect(len * (i + .5f * s), len * .5f * s, len * (i + 1 - .5f * s), len * (1 - .5f * s));
                    m_drawing.DrawRoundedRectangle(strokeRect, m_zoneWindows[i] == zoneCurrentWindow ? highlightFrameColor : frameColor, len * s);
                }
            }

            m_drawing.EndDraw();
        }

        EndPaint(m_window, &paint);
    }
};

class HiddenWindow : public Window
{
public:
    HiddenWindow(HINSTANCE hinstance) :
        Window(hinstance, nullptr, 0, WS_EX_TOOLWINDOW, Rect(0, 0, 0, 0), 0, 0, 0, SW_HIDE)
    {
    }

    void HideWindowFromTaskbar(HWND window)
    {
        HWND val = *this;
        SetWindowLongPtr(window, GWLP_HWNDPARENT, (LONG_PTR)val);
    }
};

class TabsZoneTitleBar : public IZoneTitleBar
{
private:
    static constexpr int c_style = WS_OVERLAPPED | WS_CAPTION | WS_THICKFRAME;
    static constexpr int c_exStyle = WS_EX_NOREDIRECTIONBITMAP;
    static constexpr int c_widthFactor = 4;

public:
    TabsZoneTitleBar(HINSTANCE hinstance, Rect zone, UINT dpi) noexcept :
        m_hiddenWindow(hinstance),
        m_zoneCurrentWindow(NULL),
        m_dpi(dpi),
        m_window(
            hinstance,
            [this](HWND window, UINT message, WPARAM wParam, LPARAM lParam) { return WndProc(window, message, wParam, lParam); },
            c_style,
            c_exStyle,
            ResetRect(zone),
            NULL,
            NULL,
            NULL)
    {
    }

    void UpdateZoneWindows(std::vector<HWND> zoneWindows) override
    {
        m_zoneWindows = zoneWindows;
        ReadjustPos();
    }

    void ReadjustPos() override
    {
        auto zoneCurrentWindow = GetZoneCurrentWindow();

        // Put the zone title bar just below the zone current window
        if (zoneCurrentWindow != NULL)
        {
            m_zoneCurrentWindow = zoneCurrentWindow;

            // Put the zone title bar just below the zoneCurrentWindow
            SetWindowPos(m_window, zoneCurrentWindow, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOOWNERZORDER | SWP_NOSIZE);
        }

        Render(m_window);
    }

    int GetHeight() const override { return m_height; }

protected:
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
            HANDLE_MSG(window, WM_CREATE, Init);
            HANDLE_MSG(window, WM_NCCALCSIZE, CalcNonClientSize);
            HANDLE_MSG(window, WM_WINDOWPOSCHANGING, WindowPosChanging);
            HANDLE_MSG(window, WM_DWMNCRENDERINGCHANGED, DwmNonClientRenderingChanged);
            HANDLE_MSG(window, WM_PAINT, Render);
            HANDLE_MSG(window, WM_LBUTTONDOWN, Click);
        default:
            return DefWindowProcW(window, message, wParam, lParam);
        }
    }

    bool Init(HWND hwnd, LPCREATESTRUCT)
    {
        // Hide from taskbar
        m_hiddenWindow.HideWindowFromTaskbar(hwnd);

        // Disable transitions
        BOOL disable = TRUE;
        DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, &disable, sizeof(disable));

        // Extend frame (twice the size needed)
        MARGINS margins = { 0, 0, 2 * m_height, 0 };
        DwmExtendFrameIntoClientArea(hwnd, &margins);

        // Update frame
        SetWindowPos(hwnd, nullptr, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);

        // Initialize drawing
        m_drawing.Init(hwnd);

        return true;
    }

    UINT CalcNonClientSize(HWND hwnd, BOOL calc, NCCALCSIZE_PARAMS* info)
    {
        if (!calc)
        {
            return FORWARD_WM_NCCALCSIZE(hwnd, calc, info, DefWindowProcW);
        }

        auto xBorder = GetSystemMetricsForDpi(SM_CXFRAME, m_dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, m_dpi);
        auto yBorder = GetSystemMetricsForDpi(SM_CYFRAME, m_dpi) + GetSystemMetricsForDpi(SM_CYEDGE, m_dpi);

        auto& coordinates = info->rgrc[0];
        coordinates.left += xBorder;
        coordinates.right -= xBorder;
        coordinates.bottom -= yBorder;

        return 0;
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

    BOOL WindowPosChanging(HWND hwnd, WINDOWPOS* pos)
    {
        if (m_zoneCurrentWindow != NULL)
        {
            // If changing the Z order and the change does not put it in the correct place
            if (pos->hwndInsertAfter != m_zoneCurrentWindow)
            {
                // Abort the change
                pos->flags |= SWP_NOZORDER;
            }
        }

        return true;
    }

    void Render(HWND hwnd)
    {
        PAINTSTRUCT paint;
        BeginPaint(m_window, &paint);

        auto backColor = Drawing::ConvertColor(GetSysColor(COLOR_WINDOW));
        auto frameColor = Drawing::ConvertColor(GetSysColor(COLOR_3DFACE));
        auto textColor = Drawing::ConvertColor(GetSysColor(COLOR_BTNTEXT));

        auto highlightFrameColor = Drawing::ConvertColor(GetSysColor(COLOR_HIGHLIGHT));
        auto highlightTextColor = Drawing::ConvertColor(GetSysColor(COLOR_HIGHLIGHTTEXT));

        auto captionHeight = GetSystemMetricsForDpi(SM_CYCAPTION, m_dpi);

        auto zoneCurrentWindow = GetZoneCurrentWindow();

        NONCLIENTMETRICS metrics{};
        metrics.cbSize = sizeof(metrics);
        SystemParametersInfoForDpi(SPI_GETNONCLIENTMETRICS, sizeof(NONCLIENTMETRICS), &metrics, 0, m_dpi);
        TCHAR text[100]{};

        if (m_drawing)
        {
            m_drawing.BeginDraw();

            {
                auto textFormat = m_drawing.CreateTextFormat(
                    metrics.lfCaptionFont.lfFaceName,
                    float(-metrics.lfCaptionFont.lfHeight),
                    (DWRITE_FONT_WEIGHT)metrics.lfCaptionFont.lfWeight);

                for (auto i = 0; i < m_zoneWindows.size(); ++i)
                {
                    auto xOffset = m_height * (1 + c_widthFactor * i);
                    auto yOffset = 0;

                    auto backMargin = (m_height - captionHeight) * .4f;
                    auto backHeight = m_height - 2 * backMargin;
                    auto backWidth = c_widthFactor * m_height - 2 * backMargin;
                    auto backRect = D2D1::RectF(
                        float(xOffset + backMargin),
                        float(yOffset + backMargin),
                        float(xOffset + backMargin + backWidth),
                        float(yOffset + backMargin + backHeight));
                    m_drawing.FillRoundedRectangle(backRect, m_zoneWindows[i] == zoneCurrentWindow ? highlightFrameColor : frameColor);

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
                            float(xOffset + c_widthFactor * m_height - textMargin),
                            float(yOffset + iconMargin + captionHeight));

                        text[0] = TEXT('\0');
                        GetWindowText(m_zoneWindows[i], text, ARRAYSIZE(text));
                        m_drawing.DrawTextTrim(text, textFormat.get(), textRect, m_zoneWindows[i] == zoneCurrentWindow ? highlightTextColor : textColor);
                    }
                }
            }

            m_drawing.EndDraw();
        }

        EndPaint(m_window, &paint);
    }

    void Click(HWND hwnd, BOOL doubleClick, int x, int y, UINT keyFlags)
    {
        auto len = m_height;
        if (len == 0 || x < len)
        {
            return;
        }

        auto i = (x - len) / (len * c_widthFactor);

        if (i < m_zoneWindows.size())
        {
            SwitchToWindow(m_zoneWindows[i]);
        }
    }

    HWND GetZoneCurrentWindow()
    {
        return GetWindowAboveAllOthers(m_zoneWindows);
    }

    Rect ResetRect(Rect zone)
    {
        m_zone = zone;

        RECT rect{};
        AdjustWindowRectExForDpi(&rect, c_style, FALSE, c_exStyle, m_dpi);

        auto height = -rect.top;
        m_height = height > zone.height() ? 0 : height;

        return zone;
    }

protected:
    HiddenWindow m_hiddenWindow;
    HWND m_zoneCurrentWindow;
    UINT m_dpi;
    Rect m_zone;
    int m_height;
    std::vector<HWND> m_zoneWindows;
    CompositionDrawing m_drawing;
    Window m_window;
};

std::unique_ptr<IZoneTitleBar> MakeZoneTitleBar(ZoneTitleBarStyle style, HINSTANCE hinstance, Rect zone, UINT dpi)
{
    switch (style)
    {
    case ZoneTitleBarStyle::Numbers:
        return std::make_unique<NumbersZoneTitleBar>(hinstance, zone, dpi);

    case ZoneTitleBarStyle::Icons:
        return std::make_unique<IconsZoneTitleBar>(hinstance, zone, dpi);

    case ZoneTitleBarStyle::Tabs:
        return std::make_unique<TabsZoneTitleBar>(hinstance, zone, dpi);

    case ZoneTitleBarStyle::None:
    default:
        return std::make_unique<NoZoneTitleBar>();
    }
}
