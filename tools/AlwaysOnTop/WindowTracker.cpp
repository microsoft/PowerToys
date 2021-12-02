#include "pch.h"
#include "WindowTracker.h"

// Non-Localizable strings
namespace NonLocalizable
{
	const wchar_t ToolWindowClassName[] = L"AlwaysOnTop_Border";
}

std::optional<RECT> GetFrameRect(HWND window)
{
    RECT rect;
    if (!GetWindowRect(window, &rect))
    {
        return std::nullopt;
    }

    rect.top -= 5;
    return rect;
}

WindowTracker::WindowTracker(HWND window)
	: m_window(nullptr)
	, m_trackingWindow(window)
	, m_frameDrawer(nullptr)
{
}

WindowTracker::WindowTracker(WindowTracker&& other)
    : m_window(other.m_window)
    , m_trackingWindow(other.m_trackingWindow)
    , m_frameDrawer(std::move(other.m_frameDrawer))
{
}

WindowTracker::~WindowTracker()
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

bool WindowTracker::Init(HINSTANCE hinstance)
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

    m_frameDrawer = FrameDrawer::Create(m_window);
    if (!m_frameDrawer)
    {
        return false;
    }

    RECT frameRect{ 0, 0, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top };
    m_frameDrawer->SetBorderRect(frameRect);
    m_frameDrawer->Show();

    return true;
}

void WindowTracker::DrawFrame() const
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

void WindowTracker::Hide() const
{
    m_frameDrawer->Hide();
}

LRESULT WindowTracker::WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept
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
