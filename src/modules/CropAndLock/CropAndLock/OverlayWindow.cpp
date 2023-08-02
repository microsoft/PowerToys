#include "pch.h"
#include "OverlayWindow.h"

namespace winrt
{
    using namespace Windows::UI;
    using namespace Windows::UI::Composition;
}

namespace util
{
    using namespace robmikh::common::desktop;
}

const std::wstring OverlayWindow::ClassName = L"CropAndLock.OverlayWindow";
const float OverlayWindow::BorderThickness = 5;
std::once_flag OverlayWindowClassRegistration;

bool IsPointWithinRect(POINT const& point, RECT const& rect)
{
    return point.x >= rect.left && point.x <= rect.right && point.y >= rect.top && point.y <= rect.bottom;
}

void OverlayWindow::RegisterWindowClass()
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));
    WNDCLASSEXW wcex = {};
    wcex.cbSize = sizeof(wcex);
    wcex.lpfnWndProc = WndProc;
    wcex.hInstance = instance;
    wcex.hIcon = LoadIconW(instance, IDI_APPLICATION);
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wcex.lpszClassName = ClassName.c_str();
    wcex.hIconSm = LoadIconW(instance, IDI_APPLICATION);
    winrt::check_bool(RegisterClassExW(&wcex));
}

OverlayWindow::OverlayWindow(
    winrt::Compositor const& compositor, 
    HWND windowToCrop, 
    std::function<void(HWND, RECT)> windowCropped)
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));

    std::call_once(OverlayWindowClassRegistration, []() { RegisterWindowClass(); });

    auto exStyle = WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_TOPMOST;
    auto style = WS_POPUP;

    // Get the union of all displays
    auto displaysRect = ComputeAllDisplaysUnion();

    // Create our window
    winrt::check_bool(CreateWindowExW(exStyle, ClassName.c_str(), L"", style,
        displaysRect.left, displaysRect.top, displaysRect.right - displaysRect.left, displaysRect.bottom - displaysRect.top, nullptr, nullptr, instance, this));
    WINRT_ASSERT(m_window);

    // Load cursors
    m_standardCursor.reset(winrt::check_pointer(LoadCursorW(nullptr, IDC_ARROW)));
    m_crosshairCursor.reset(winrt::check_pointer(LoadCursorW(nullptr, IDC_CROSS)));
    m_cursorType = CursorType::Standard;

    // Setup the visual tree
    m_compositor = compositor;
    m_target = CreateWindowTarget(m_compositor);
    m_rootVisual = m_compositor.CreateContainerVisual();
    m_shadeVisual = m_compositor.CreateSpriteVisual();
    m_windowAreaVisual = m_compositor.CreateContainerVisual();
    m_selectionVisual = m_compositor.CreateSpriteVisual();

    m_target.Root(m_rootVisual);
    auto children = m_rootVisual.Children();
    children.InsertAtBottom(m_shadeVisual);
    children.InsertAtTop(m_windowAreaVisual);
    m_windowAreaVisual.Children().InsertAtTop(m_selectionVisual);

    m_rootVisual.RelativeSizeAdjustment({ 1, 1 });
    m_shadeBrush = m_compositor.CreateNineGridBrush();
    m_shadeBrush.IsCenterHollow(true);
    m_shadeBrush.Source(m_compositor.CreateColorBrush(winrt::Color{ 255, 0, 0, 0 }));
    m_shadeVisual.Brush(m_shadeBrush);
    m_shadeVisual.Opacity(0.6f);
    m_shadeVisual.RelativeSizeAdjustment({ 1, 1 });
    auto selectionBrush = m_compositor.CreateNineGridBrush();
    selectionBrush.SetInsets(BorderThickness);
    selectionBrush.IsCenterHollow(true);
    selectionBrush.Source(m_compositor.CreateColorBrush(winrt::Color{ 255, 255, 0, 0 }));
    m_selectionVisual.Brush(selectionBrush);

    WINRT_VERIFY(windowToCrop != nullptr);
    m_currentWindow = windowToCrop;
    SetupOverlay();
    m_windowCropped = windowCropped;

    ShowWindow(m_window, SW_SHOW);
    UpdateWindow(m_window);
    SetForegroundWindow(m_window);
}

void OverlayWindow::SetupOverlay()
{
    ResetCrop();

    // Get the client bounds of the target window
    auto windowBounds = ClientAreaInScreenSpace(m_currentWindow);

    // Get the union of all displays
    auto displaysRect = ComputeAllDisplaysUnion();

    // Before we can use the window bounds, we need to 
    // shift the origin to the top-left most point.
    m_currentWindowAreaBounds.left = windowBounds.left - displaysRect.left;
    m_currentWindowAreaBounds.top = windowBounds.top - displaysRect.top;
    m_currentWindowAreaBounds.right = m_currentWindowAreaBounds.left + (windowBounds.right - windowBounds.left);
    m_currentWindowAreaBounds.bottom = m_currentWindowAreaBounds.top + (windowBounds.bottom - windowBounds.top);

    auto windowLeft = static_cast<float>(m_currentWindowAreaBounds.left);
    auto windowTop = static_cast<float>(m_currentWindowAreaBounds.top);
    auto windowWidth = static_cast<float>(windowBounds.right - windowBounds.left);
    auto windowHeight = static_cast<float>(windowBounds.bottom - windowBounds.top);

    // Change the shade brush to match the window bounds
    // We need to make sure the values are non-negative, as they are invalid insets. We
    // can sometimes get negative values for the left and top when windows are maximized.
    m_shadeBrush.LeftInset(std::max(windowLeft, 0.0f));
    m_shadeBrush.TopInset(std::max(windowTop, 0.0f));
    m_shadeBrush.RightInset(std::max(static_cast<float>(displaysRect.right - windowBounds.right), 0.0f));
    m_shadeBrush.BottomInset(std::max(static_cast<float>(displaysRect.bottom - windowBounds.bottom), 0.0f));

    // Change the window area visual to match the window bounds
    m_windowAreaVisual.Offset({ windowLeft, windowTop, 0 });
    m_windowAreaVisual.Size({ windowWidth, windowHeight });

    // Reset the selection visual
    m_selectionVisual.Offset({ 0, 0, 0 });
    m_selectionVisual.Size({ 0, 0 });
}

LRESULT OverlayWindow::MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam)
{
    switch (message)
    {
    case WM_DESTROY:
        break;
    case WM_SETCURSOR:
        return OnSetCursor();
    case WM_KEYUP:
    {
        auto key = static_cast<uint32_t>(wparam);
        if (key == VK_ESCAPE)
        {
            DestroyWindow(m_window);
        }
    }
    break;
    case WM_LBUTTONDOWN:
    {
        auto xPos = GET_X_LPARAM(lparam);
        auto yPos = GET_Y_LPARAM(lparam);
        OnLeftButtonDown(xPos, yPos);
    }
    break;
    case WM_LBUTTONUP:
    {
        auto xPos = GET_X_LPARAM(lparam);
        auto yPos = GET_Y_LPARAM(lparam);
        OnLeftButtonUp(xPos, yPos);
    }
    break;
    case WM_MOUSEMOVE:
    {
        auto xPos = GET_X_LPARAM(lparam);
        auto yPos = GET_Y_LPARAM(lparam);
        OnMouseMove(xPos, yPos);
    }
    break;
    default:
        return base_type::MessageHandler(message, wparam, lparam);
    }
    return 0;
}

void OverlayWindow::ResetCrop()
{
    m_cropStatus = CropStatus::None;
    m_startPosition = {};
    m_cropRect = {};
}

bool OverlayWindow::OnSetCursor()
{
    switch (m_cursorType)
    {
    case CursorType::Standard:
        SetCursor(m_standardCursor.get());
        return true;
    case CursorType::Crosshair:
        SetCursor(m_crosshairCursor.get());
        return true;
    default:
        return false;
    }
}

void OverlayWindow::OnLeftButtonDown(int x, int y)
{
    if (m_cropStatus == CropStatus::None)
    {
        if (!IsPointWithinRect({ x, y }, m_currentWindowAreaBounds))
        {
            DestroyWindow(m_window);
            return;
        }

        m_cropStatus = CropStatus::Ongoing;

        x -= m_currentWindowAreaBounds.left;
        y -= m_currentWindowAreaBounds.top;

        m_selectionVisual.Offset({ x - BorderThickness, y - BorderThickness, 0 });
        m_startPosition = { x, y };
    }
}

void OverlayWindow::OnLeftButtonUp(int x, int y)
{
    if (m_cropStatus == CropStatus::Ongoing)
    {
        m_cropStatus = CropStatus::Completed;
        m_cursorType = CursorType::Standard;

        // For debugging, it's easier if the window doesn't block the screen after this point
        ShowWindow(m_window, SW_HIDE);

        if (x < m_currentWindowAreaBounds.left)
        {
            x = m_currentWindowAreaBounds.left;
        }
        else if (x > m_currentWindowAreaBounds.right)
        {
            x = m_currentWindowAreaBounds.right;
        }

        if (y < m_currentWindowAreaBounds.top)
        {
            y = m_currentWindowAreaBounds.top;
        }
        else if (y > m_currentWindowAreaBounds.bottom)
        {
            y = m_currentWindowAreaBounds.bottom;
        }

        x -= m_currentWindowAreaBounds.left;
        y -= m_currentWindowAreaBounds.top;

        // Compute our crop rect
        if (x < m_startPosition.x)
        {
            m_cropRect.left = x;
            m_cropRect.right = m_startPosition.x;
        }
        else
        {
            m_cropRect.left = m_startPosition.x;
            m_cropRect.right = x;
        }
        if (y < m_startPosition.y)
        {
            m_cropRect.top = y;
            m_cropRect.bottom = m_startPosition.y;
        }
        else
        {
            m_cropRect.top = m_startPosition.y;
            m_cropRect.bottom = y;
        }

        // Exit if the rect is empty
        if (m_cropRect.right - m_cropRect.left == 0 || m_cropRect.bottom - m_cropRect.top == 0)
        {
            DestroyWindow(m_window);
            return;
        } 

        // Fire the callback
        if (m_windowCropped != nullptr)
        {
            m_windowCropped(m_currentWindow, m_cropRect);
        }
        DestroyWindow(m_window);
    }
}

void OverlayWindow::OnMouseMove(int x, int y)
{
    if (m_cropStatus == CropStatus::None)
    {
        if (IsPointWithinRect({ x, y }, m_currentWindowAreaBounds))
        {
            m_cursorType = CursorType::Crosshair;
        }
        else
        {
            m_cursorType = CursorType::Standard;
        }
    } 
    else if (m_cropStatus == CropStatus::Ongoing)
    {
        if (x < m_currentWindowAreaBounds.left)
        {
            x = m_currentWindowAreaBounds.left;
        }
        else if (x > m_currentWindowAreaBounds.right)
        {
            x = m_currentWindowAreaBounds.right;
        }

        if (y < m_currentWindowAreaBounds.top)
        {
            y = m_currentWindowAreaBounds.top;
        }
        else if (y > m_currentWindowAreaBounds.bottom)
        {
            y = m_currentWindowAreaBounds.bottom;
        }

        x -= m_currentWindowAreaBounds.left;
        y -= m_currentWindowAreaBounds.top;

        auto offset = m_selectionVisual.Offset();
        auto size = m_selectionVisual.Size();

        if (x < m_startPosition.x)
        {
            offset.x = x - BorderThickness;
            size.x = (m_startPosition.x - x) + (2 * BorderThickness);
        }
        else
        {
            size.x = (x - m_startPosition.x) + (2 * BorderThickness);
        }

        if (y < m_startPosition.y)
        {
            offset.y = y - BorderThickness;
            size.y = (m_startPosition.y - y) + (2 * BorderThickness);
        }
        else
        {
            size.y = (y - m_startPosition.y) + (2 * BorderThickness);
        }

        m_selectionVisual.Offset(offset);
        m_selectionVisual.Size(size);
    }
}
