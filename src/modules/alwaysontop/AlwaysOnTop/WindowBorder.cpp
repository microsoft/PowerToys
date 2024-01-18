#include "pch.h"
#include "WindowBorder.h"

#include <dwmapi.h>
#include "winrt/Windows.Foundation.h"

#include <FrameDrawer.h>
#include <ScalingUtils.h>
#include <Settings.h>
#include <WindowCornersUtil.h>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t ToolWindowClassName[] = L"AlwaysOnTop_Border";
}

std::optional<RECT> GetFrameRect(HWND window)
{
    RECT rect;
    if (!SUCCEEDED(DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(rect))))
    {
        return std::nullopt;
    }

    int border = AlwaysOnTopSettings::settings().frameThickness;
    rect.top -= border;
    rect.left -= border;
    rect.right += border;
    rect.bottom += border;

    return rect;
}

WindowBorder::WindowBorder(HWND window) :
    SettingsObserver({ SettingId::FrameColor, SettingId::FrameThickness, SettingId::FrameAccentColor, SettingId::FrameOpacity, SettingId::RoundCornersEnabled }),
    m_window(nullptr),
    m_trackingWindow(window),
    m_frameDrawer(nullptr)
{
}

WindowBorder::~WindowBorder()
{
    if (m_frameDrawer)
    {
        m_frameDrawer->Hide();
        m_frameDrawer = nullptr;
    }

    if (m_window)
    {
        SetWindowLongPtrW(m_window, GWLP_USERDATA, 0);
        DestroyWindow(m_window);
    }
}

std::unique_ptr<WindowBorder> WindowBorder::Create(HWND window, HINSTANCE hinstance)
{
    auto self = std::unique_ptr<WindowBorder>(new WindowBorder(window));
    if (self->Init(hinstance))
    {
        return self;
    }

    return nullptr;
}

namespace
{
    constexpr uint32_t REFRESH_BORDER_TIMER_ID = 123;
    constexpr uint32_t REFRESH_BORDER_INTERVAL = 100;
}

bool WindowBorder::Init(HINSTANCE hinstance)
{
    if (!m_trackingWindow)
    {
        return false;
    }

    auto windowRectOpt = GetFrameRect(m_trackingWindow);
    if (!windowRectOpt.has_value())
    {
        return false;
    }

    RECT windowRect = windowRectOpt.value();

    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = hinstance;
    wcex.lpszClassName = NonLocalizable::ToolWindowClassName;
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassExW(&wcex);

    m_window = CreateWindowExW(WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW
        , NonLocalizable::ToolWindowClassName
        , L""
        , WS_POPUP | WS_DISABLED
        , windowRect.left
        , windowRect.top
        , windowRect.right - windowRect.left
        , windowRect.bottom - windowRect.top
        , nullptr
        , nullptr
        , hinstance
        , this);

    if (!m_window)
    {
        return false;
    }

    // make window transparent
    int const pos = -GetSystemMetrics(SM_CXVIRTUALSCREEN) - 8;
    if (wil::unique_hrgn hrgn{ CreateRectRgn(pos, 0, (pos + 1), 1) })
    {
        DWM_BLURBEHIND bh = { DWM_BB_ENABLE | DWM_BB_BLURREGION, TRUE, hrgn.get(), FALSE };
        DwmEnableBlurBehindWindow(m_window, &bh);
    }

    if (!SetLayeredWindowAttributes(m_window, RGB(0, 0, 0), 0, LWA_COLORKEY))
    {
        return false;
    }

    if (!SetLayeredWindowAttributes(m_window, 0, 255, LWA_ALPHA))
    {
        return false;
    }

    // set position of the border-window behind the tracking window
    // helps to prevent border overlapping (happens after turning borders off and on)
    SetWindowPos(m_trackingWindow
        , m_window
        , windowRect.left
        , windowRect.top
        , windowRect.right - windowRect.left
        , windowRect.bottom - windowRect.top
        , SWP_NOMOVE | SWP_NOSIZE);

    BOOL val = TRUE;
    DwmSetWindowAttribute(m_window, DWMWA_EXCLUDED_FROM_PEEK, &val, sizeof(val));

    m_frameDrawer = FrameDrawer::Create(m_window);
    if (!m_frameDrawer)
    {
        return false;
    }

    m_frameDrawer->Show();
    UpdateBorderPosition();
    UpdateBorderProperties();
    m_timer_id = SetTimer(m_window, REFRESH_BORDER_TIMER_ID, REFRESH_BORDER_INTERVAL, nullptr);

    return true;
}

void WindowBorder::UpdateBorderPosition() const
{
    if (!m_trackingWindow)
    {
        return;
    }

    auto rectOpt = GetFrameRect(m_trackingWindow);
    if (!rectOpt.has_value())
    {
        m_frameDrawer->Hide();
        return;
    }

    RECT rect = rectOpt.value();
    SetWindowPos(m_window, m_trackingWindow, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, SWP_NOREDRAW | SWP_NOACTIVATE);
}

void WindowBorder::UpdateBorderProperties() const
{
    if (!m_trackingWindow || !m_frameDrawer)
    {
        return;
    }

    auto windowRectOpt = GetFrameRect(m_trackingWindow);
    if (!windowRectOpt.has_value())
    {
        return;
    }

    const RECT windowRect = windowRectOpt.value();
    SetWindowPos(m_window, m_trackingWindow, windowRect.left, windowRect.top, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top, SWP_NOREDRAW | SWP_NOACTIVATE);

    RECT frameRect{ 0, 0, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top };

    COLORREF color;
    if (AlwaysOnTopSettings::settings().frameAccentColor)
    {
        winrt::Windows::UI::ViewManagement::UISettings settings;
        auto accentValue = settings.GetColorValue(winrt::Windows::UI::ViewManagement::UIColorType::Accent);
        color = RGB(accentValue.R, accentValue.G, accentValue.B);
    }
    else
    {
        color = AlwaysOnTopSettings::settings().frameColor;
    }

    float opacity = AlwaysOnTopSettings::settings().frameOpacity / 100.0f;
    float scalingFactor = ScalingUtils::ScalingFactor(m_trackingWindow);
    float thickness = AlwaysOnTopSettings::settings().frameThickness * scalingFactor;
    float cornerRadius = 0.0;
    if (AlwaysOnTopSettings::settings().roundCornersEnabled)
    {
        cornerRadius = WindowCornerUtils::CornersRadius(m_trackingWindow) * scalingFactor;
    }
    
    m_frameDrawer->SetBorderRect(frameRect, color, opacity, static_cast<int>(thickness), cornerRadius);
}

LRESULT WindowBorder::WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_TIMER:
    {
        switch (wparam)
        {
        case REFRESH_BORDER_TIMER_ID:
            KillTimer(m_window, m_timer_id);
            m_timer_id = SetTimer(m_window, REFRESH_BORDER_TIMER_ID, REFRESH_BORDER_INTERVAL, nullptr);
            UpdateBorderPosition();
            UpdateBorderProperties();
            break;
        }
        break;
    }
    case WM_NCDESTROY:
    {
        KillTimer(m_window, m_timer_id);
        ::DefWindowProc(m_window, message, wparam, lparam);
        SetWindowLongPtr(m_window, GWLP_USERDATA, 0);
    }
    break;

    case WM_ERASEBKGND:
        return TRUE;

    // prevent from beeping if the border was clicked
    case WM_SETCURSOR:
        return TRUE;

    default:
    {
        return DefWindowProc(m_window, message, wparam, lparam);
    }
    }
    return FALSE;
}

void WindowBorder::SettingsUpdate(SettingId id)
{
    if (!AlwaysOnTopSettings::settings().enableFrame)
    {
        return;
    }

    auto windowRectOpt = GetFrameRect(m_trackingWindow);
    if (!windowRectOpt.has_value())
    {
        return;
    }

    switch (id)
    {
    case SettingId::FrameThickness:
    {
        UpdateBorderPosition();
        UpdateBorderProperties();
    }
    break;

    case SettingId::FrameColor:
    case SettingId::FrameAccentColor:
    case SettingId::FrameOpacity:
    case SettingId::RoundCornersEnabled:
    {
        UpdateBorderProperties();
    }
    break;

    default:
        break;
    }
}
