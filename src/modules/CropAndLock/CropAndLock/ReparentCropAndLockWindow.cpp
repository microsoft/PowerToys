#include "pch.h"
#include "ReparentCropAndLockWindow.h"

const std::wstring ReparentCropAndLockWindow::ClassName = L"CropAndLock.ReparentCropAndLockWindow";
std::once_flag ReparentCropAndLockWindowClassRegistration;

void ReparentCropAndLockWindow::RegisterWindowClass()
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));
    WNDCLASSEXW wcex = {};
    wcex.cbSize = sizeof(wcex);
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = WndProc;
    wcex.hInstance = instance;
    wcex.hIcon = LoadIconW(instance, IDI_APPLICATION);
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wcex.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    wcex.lpszClassName = ClassName.c_str();
    wcex.hIconSm = LoadIconW(wcex.hInstance, IDI_APPLICATION);
    winrt::check_bool(RegisterClassExW(&wcex));
}

ReparentCropAndLockWindow::ReparentCropAndLockWindow(std::wstring const& titleString, int width, int height)
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));

    std::call_once(ReparentCropAndLockWindowClassRegistration, []() { RegisterWindowClass(); });

    auto exStyle = 0;
    auto style = WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN;
    style &= ~(WS_MAXIMIZEBOX | WS_THICKFRAME);

    RECT rect = { 0, 0, width, height};
    winrt::check_bool(AdjustWindowRectEx(&rect, style, false, exStyle));
    auto adjustedWidth = rect.right - rect.left;
    auto adjustedHeight = rect.bottom - rect.top;

    winrt::check_bool(CreateWindowExW(exStyle, ClassName.c_str(), titleString.c_str(), style,
        CW_USEDEFAULT, CW_USEDEFAULT, adjustedWidth, adjustedHeight, nullptr, nullptr, instance, this));
    WINRT_ASSERT(m_window);

    m_childWindow = std::make_unique<ChildWindow>(width, height, m_window);
}

ReparentCropAndLockWindow::~ReparentCropAndLockWindow()
{
    DisconnectTarget();
    DestroyWindow(m_window);
}

LRESULT ReparentCropAndLockWindow::MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam)
{
    switch (message)
    {
    case WM_DESTROY:
        if (m_closedCallback != nullptr && !m_destroyed)
        {
            m_destroyed = true;
            m_closedCallback(m_window);
        }
        break;
    case WM_MOUSEACTIVATE:
        if (m_currentTarget != nullptr && GetForegroundWindow() != m_currentTarget)
        {
            SetForegroundWindow(m_currentTarget);
        }
        return MA_NOACTIVATE;
    case WM_ACTIVATE:
        if (static_cast<DWORD>(wparam) == WA_ACTIVE)
        {
            if (m_currentTarget != nullptr)
            {
                SetForegroundWindow(m_currentTarget);
            }
        }
        break;
    case WM_DPICHANGED:
        break;
    default:
        return base_type::MessageHandler(message, wparam, lparam);
    }
    return 0;
}

void ReparentCropAndLockWindow::CropAndLock(HWND windowToCrop, RECT cropRect)
{
    DisconnectTarget();
    m_currentTarget = windowToCrop;

    // Save original state
    SaveOriginalState();

    RECT windowRect = {};
    winrt::check_bool(GetWindowRect(m_currentTarget, &windowRect));
    auto clientRect = ClientAreaInScreenSpace(m_currentTarget);

    WINDOWPLACEMENT windowPlacement = { sizeof(windowPlacement) };
    GetWindowPlacement(m_currentTarget, &windowPlacement);
    bool isMaximized = (windowPlacement.showCmd == SW_SHOWMAXIMIZED);

    auto diffX = clientRect.left - windowRect.left;
    auto diffY = clientRect.top - windowRect.top;

    if (isMaximized)
    {
        MONITORINFO mi = { sizeof(mi) };
        GetMonitorInfo(MonitorFromWindow(m_currentTarget, MONITOR_DEFAULTTONEAREST), &mi);

        diffX = mi.rcWork.left - windowRect.left;
        diffY = mi.rcWork.top - windowRect.top;
    }

    auto adjustedCropRect = cropRect;
    adjustedCropRect.left += diffX;
    adjustedCropRect.top += diffY;
    adjustedCropRect.right += diffX;
    adjustedCropRect.bottom += diffY;
    cropRect = adjustedCropRect;

    auto newX = adjustedCropRect.left + windowRect.left;
    auto newY = adjustedCropRect.top + windowRect.top;

    auto monitor = winrt::check_pointer(MonitorFromWindow(m_currentTarget, MONITOR_DEFAULTTONULL));
    uint32_t dpiX = 0;
    uint32_t dpiY = 0;
    winrt::check_hresult(GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, &dpiX, &dpiY));
    uint32_t dpi = dpiX > dpiY ? dpiX : dpiY;

    // Reconfigure our window
    auto width = cropRect.right - cropRect.left;
    auto height = cropRect.bottom - cropRect.top;
    windowRect = { newX, newY, newX + width, newY + height };
    auto exStyle = static_cast<DWORD>(GetWindowLongPtrW(m_window, GWL_EXSTYLE));
    auto style = static_cast<DWORD>(GetWindowLongPtrW(m_window, GWL_STYLE));
    winrt::check_bool(AdjustWindowRectExForDpi(&windowRect, style, false, exStyle, dpi));
    auto adjustedWidth = windowRect.right - windowRect.left;
    auto adjustedHeight = windowRect.bottom - windowRect.top;

    winrt::check_bool(SetWindowPos(m_window, HWND_TOPMOST, windowRect.left, windowRect.top, adjustedWidth, adjustedHeight, SWP_SHOWWINDOW | SWP_NOACTIVATE));
    winrt::check_bool(SetWindowPos(m_childWindow->m_window, nullptr, 0, 0, width, height, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE));

    // Reparent the target window
    SetParent(m_currentTarget, m_childWindow->m_window);
    auto targetStyle = GetWindowLongPtrW(m_currentTarget, GWL_STYLE);
    targetStyle |= WS_CHILD;
    SetWindowLongPtrW(m_currentTarget, GWL_STYLE, targetStyle);
    auto x = -cropRect.left;
    auto y = -cropRect.top;
    winrt::check_bool(SetWindowPos(m_currentTarget, nullptr, x, y, 0, 0, SWP_NOSIZE | SWP_FRAMECHANGED | SWP_NOZORDER));
}

void ReparentCropAndLockWindow::Hide()
{
    DisconnectTarget();
    ShowWindow(m_window, SW_HIDE);
}

void ReparentCropAndLockWindow::DisconnectTarget()
{
    if (m_currentTarget != nullptr)
    {
        if (!IsWindow(m_currentTarget))
        {
            // The child window was closed by other means?
            m_currentTarget = nullptr;
            return;
        }
        SetParent(m_currentTarget, nullptr);
        RestoreOriginalState();

        Sleep(50);
    }
}

void ReparentCropAndLockWindow::SaveOriginalState()
{
    if (m_currentTarget != nullptr)
    {
        originalPlacement.length = sizeof(WINDOWPLACEMENT);
        GetWindowPlacement(m_currentTarget, &originalPlacement);

        originalExStyle = GetWindowLongPtr(m_currentTarget, GWL_EXSTYLE);
        originalStyle = GetWindowLongPtr(m_currentTarget, GWL_STYLE);

        GetWindowRect(m_currentTarget, &originalRect);
    }
}

void ReparentCropAndLockWindow::RestoreOriginalState()
{
    if (m_currentTarget)
    {
        // Set the original extended style and style
        originalStyle &= ~WS_CHILD;
        SetWindowLongPtrW(m_currentTarget, GWL_EXSTYLE, originalExStyle);
        SetWindowLongPtrW(m_currentTarget, GWL_STYLE, originalStyle);

        // Restore window position and dimensions
        int width = originalRect.right - originalRect.left;
        int height = originalRect.bottom - originalRect.top;
        SetWindowPos(m_currentTarget, nullptr, originalRect.left, originalRect.top, width, height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        // Now, restore the original placement
        if (originalPlacement.showCmd != SW_SHOWMAXIMIZED)
        {
            originalPlacement.showCmd = SW_RESTORE;
            SetWindowPlacement(m_currentTarget, &originalPlacement);
        }
        else
        {
            ShowWindow(m_currentTarget, SW_SHOWMAXIMIZED);
        }
    }
}
