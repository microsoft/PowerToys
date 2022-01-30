#include "pch.h"
#include "ZoneTitleBar.h"
#include <set>
#include <windowsx.h>

using namespace FancyZonesUtils;


class NoZoneTitleBar : public IZoneTitleBar
{
public:
    NoZoneTitleBar() noexcept {}

    void UpdateZoneWindows(std::vector<HWND> zoneWindows) override {}

    void ReadjustPos() override {}

    int GetHeight() const override { return 0; }
};

class ZoneTitleBar : public IZoneTitleBar
{
public:
    ZoneTitleBar(HINSTANCE hinstance, Rect zone) noexcept :
        m_window(
            hinstance,
            [this](HWND window, UINT message, WPARAM wParam, LPARAM lParam) { return WndProc(window, message, wParam, lParam); },
            WS_POPUP,
            WS_EX_TOOLWINDOW,
            ResetRect(zone),
            NULL,
            NULL,
            NULL),
        m_drawing(m_window)
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
            HANDLE_MSG(window, WM_PAINT, Render);
            HANDLE_MSG(window, WM_LBUTTONDOWN, Click);
        default:
            return DefWindowProcW(window, message, wParam, lParam);
        }
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
        if (m_zoneWindows.empty())
        {
            return NULL;
        }

        std::set<HWND> zoneWindows(m_zoneWindows.begin(), m_zoneWindows.end());

        // Get the window above all others
        HWND max = NULL;
        for (HWND current = m_zoneWindows.front(); !zoneWindows.empty() && current != NULL; current = GetWindow(current, GW_HWNDPREV))
        {
            auto i = zoneWindows.find(current);
            if (i != zoneWindows.end())
            {
                max = current;
                zoneWindows.erase(i);
            }
        }

        return max;
    }

    Rect ResetRect(Rect zone)
    {
        int height = GetSystemMetrics(SM_CYHSCROLL);
        m_height = height > zone.height() ? 0 : height;
        return Rect(zone.position(), zone.width(), m_height);
    }

protected:
    int m_height;
    std::vector<HWND> m_zoneWindows;
    Window m_window;
    Drawing m_drawing;
};

class NumbersZoneTitleBar : public ZoneTitleBar
{
public:
    NumbersZoneTitleBar(HINSTANCE hinstance, Rect zone) noexcept :
        ZoneTitleBar(hinstance, zone)
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

class IconsZoneTitleBar : public ZoneTitleBar
{
public:
    IconsZoneTitleBar(HINSTANCE hinstance, Rect zone) noexcept :
        ZoneTitleBar(hinstance, zone)
    {
    }

    void DrawIcon(HWND window, const D2D1_RECT_F& rect)
    {
        auto icon = (HICON)SendMessageW(window, WM_GETICON, ICON_SMALL2, 0);
        if (icon == nullptr)
        {
            icon = (HICON)GetClassLongPtrW(window, GCLP_HICONSM);
        }

        if (icon != nullptr)
        {
            auto bitmap = m_drawing.CreateIcon(icon);
            m_drawing.DrawBitmap(rect, bitmap.get());
        }
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
                    DrawIcon(m_zoneWindows[i], iconRect);

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

std::unique_ptr<IZoneTitleBar> MakeZoneTitleBar(ZoneTitleBarStyle style, HINSTANCE hinstance, Rect zone)
{
    switch (style)
    {
    case ZoneTitleBarStyle::Numbers:
        return std::make_unique<NumbersZoneTitleBar>(hinstance, zone);

    case ZoneTitleBarStyle::Icons:
        return std::make_unique<IconsZoneTitleBar>(hinstance, zone);

    case ZoneTitleBarStyle::None:
    default:
        return std::make_unique<NoZoneTitleBar>();
    }
}
