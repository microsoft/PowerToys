#include "pch.h"
#include "WindowBorder.h"

#include <dwmapi.h>

#include <FrameDrawer.h>
#include <Settings.h>

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

    int border = static_cast<int>(AlwaysOnTopSettings::settings().frameThickness / 2);
    rect.top -= border;
    rect.left -= border;
    rect.right += border;
    rect.bottom += border;

    return rect;
}

WindowBorder::WindowBorder(HWND window) :
    SettingsObserver({SettingId::FrameColor, SettingId::FrameThickness}),
    m_window(nullptr), 
    m_trackingWindow(window), 
    m_frameDrawer(nullptr)
{
}

WindowBorder::WindowBorder(WindowBorder&& other) :
    SettingsObserver({ SettingId::FrameColor, SettingId::FrameThickness }),
    m_window(other.m_window), 
    m_trackingWindow(other.m_trackingWindow), 
    m_frameDrawer(std::move(other.m_frameDrawer))
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
        ShowWindow(m_window, SW_HIDE);
    }
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
        , WS_POPUP
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

    if (!SetLayeredWindowAttributes(m_window, RGB(0, 0, 0), 0, LWA_COLORKEY))
    {
        return false;
    }

    // set position of the border-window behind the tracking window
    // helps to prevent border overlapping (happens after turning borders off and on)
    SetWindowPos(m_trackingWindow
        , m_window
        , windowRect.left
        , windowRect.top
        , windowRect.right - windowRect.left - static_cast<int>(AlwaysOnTopSettings::settings().frameThickness)
        , windowRect.bottom - windowRect.top - static_cast<int>(AlwaysOnTopSettings::settings().frameThickness)
        , SWP_NOMOVE | SWP_NOSIZE);

    m_frameDrawer = FrameDrawer::Create(m_window);
    return m_frameDrawer != nullptr;
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
        return;
    }

    RECT rect = rectOpt.value();
    SetWindowPos(m_window, m_trackingWindow, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, SWP_NOREDRAW);
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

    RECT windowRect = windowRectOpt.value();
    RECT frameRect{ 0, 0, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top };
    m_frameDrawer->SetBorderRect(frameRect, AlwaysOnTopSettings::settings().frameColor, AlwaysOnTopSettings::settings().frameThickness);
}

void WindowBorder::Show() const
{
    UpdateBorderProperties();
    m_frameDrawer->Show();
}

void WindowBorder::Hide() const
{
    m_frameDrawer->Hide();
}

LRESULT WindowBorder::WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_NCDESTROY:
    {
        ::DefWindowProc(m_window, message, wparam, lparam);
        SetWindowLongPtr(m_window, GWLP_USERDATA, 0);
    }
    break;

    case WM_ERASEBKGND:
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
    {
        UpdateBorderProperties();
    }
    break;

    default:
        break;
    }
    
}
