// MouseHighlighter.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "MouseHighlighter.h"

#ifdef COMPOSITION
namespace winrt
{
    using namespace winrt::Windows::System;
    using namespace winrt::Windows::UI::Composition;
}

namespace ABI
{
    using namespace ABI::Windows::System;
    using namespace ABI::Windows::UI::Composition::Desktop;
}
#endif

void StartDrawing();
void StopDrawing();

constexpr auto m_className = L"MouseHighlighter";
constexpr auto m_windowTitle = L"MouseHighlighter";
HWND m_hwndOwner;
HWND m_hwnd;
HINSTANCE m_hinstance;
constexpr DWORD ID_SHORTCUT_ACTIVATE = 1;
constexpr DWORD ID_SHORTCUT_EXIT = 2;

winrt::DispatcherQueueController m_dispatcherQueueController{ nullptr };
winrt::Compositor m_compositor{ nullptr };
winrt::Desktop::DesktopWindowTarget m_target{ nullptr };
winrt::ContainerVisual m_root{ nullptr };
winrt::LayerVisual m_layer{ nullptr };
winrt::ShapeVisual m_shape{ nullptr };

winrt::CompositionEllipseGeometry m_cursorGeometry{ nullptr };
winrt::CompositionSpriteShape m_cursor{ nullptr };

bool visible = false;

// Possible configurable settings
float m_radius = 20.0f;

int m_fadeDelay_ms = 2000;
int m_fadeDuration_ms = 2000;

winrt::Windows::UI::Color m_leftClickColor = winrt::Windows::UI::ColorHelper::FromArgb(160, 255, 255, 0);
winrt::Windows::UI::Color m_rightClickColor = winrt::Windows::UI::ColorHelper::FromArgb(160, 0, 0, 255);



bool CreateHighlighter()
{
    try
    {
        // We need a dispatcher queue.
        DispatcherQueueOptions options =
        {
            sizeof(options),
            DQTYPE_THREAD_CURRENT,
            DQTAT_COM_ASTA,
        };
        ABI::IDispatcherQueueController* controller;
        winrt::check_hresult(CreateDispatcherQueueController(options, &controller));
        *winrt::put_abi(m_dispatcherQueueController) = controller;

        // Create the compositor for our window.
        m_compositor = winrt::Compositor();
        ABI::IDesktopWindowTarget* target;
        winrt::check_hresult(m_compositor.as<ABI::ICompositorDesktopInterop>()->CreateDesktopWindowTarget(m_hwnd, false, &target));
        *winrt::put_abi(m_target) = target;

        // Create visual root
        m_root = m_compositor.CreateContainerVisual();
        m_root.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_target.Root(m_root);

        // Create the shapes continer visual and add it to root.
        m_shape = m_compositor.CreateShapeVisual();
        m_shape.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_root.Children().InsertAtTop(m_shape);

        return true;
    } catch (...)
    {
        return false;
    }
}


void AddDrawingPoint(winrt::Windows::UI::Color color)
{
    POINT pt;
        
    // Applies DPIs.
    GetCursorPos(&pt);

    // Converts to client area of the Windows.
    ScreenToClient(m_hwnd, &pt);

    // Create circle and add it.
    auto circleGeometry = m_compositor.CreateEllipseGeometry();
    circleGeometry.Radius({ m_radius, m_radius });
    auto circleShape = m_compositor.CreateSpriteShape(circleGeometry);
    circleShape.FillBrush(m_compositor.CreateColorBrush(color));
    circleShape.Offset({ (float)pt.x, (float)pt.y });
    m_shape.Shapes().Append(circleShape);

    // Animate opacity to simulate a fage away effect.
    auto animation = m_compositor.CreateColorKeyFrameAnimation();
    animation.InsertKeyFrame(1, winrt::Windows::UI::ColorHelper::FromArgb(0, color.R, color.G, color.B));
    using timeSpan = std::chrono::duration<int, std::ratio<1, 1000>>;
    std::chrono::milliseconds duration(m_fadeDuration_ms);
    std::chrono::milliseconds delay(m_fadeDelay_ms);
    animation.Duration(timeSpan(duration));
    animation.DelayTime(timeSpan(delay));
    circleShape.FillBrush().StartAnimation(L"Color", animation);

    // TODO: We're leaking shapes for long drawing sessions.
    // Perhaps add a task to the Dispatcher every X circles to clean up.

    // Get back on top in case other Window is now the topmost.
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
        GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN), 0);
}

void ClearDrawing()
{
    m_shape.Shapes().Clear();
}

bool pressed = false;
LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode >= 0)
    {
        MSLLHOOKSTRUCT* hookData = (MSLLHOOKSTRUCT*)lParam;
        if (wParam == WM_LBUTTONDOWN)
        {
            AddDrawingPoint(m_leftClickColor);
        }
        if (wParam == WM_RBUTTONDOWN)
        {
            AddDrawingPoint(m_rightClickColor);
        }
    }
    return CallNextHookEx(0, nCode, wParam, lParam);
}

HHOOK mousehook = NULL;


void StartDrawing()
{
    visible = true;
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
        GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN), 0);
    ClearDrawing();
    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    mousehook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, m_hinstance, 0);
}

void StopDrawing()
{
    visible = false;
    ShowWindow(m_hwnd, SW_HIDE);
    UnhookWindowsHookEx(mousehook);
    ClearDrawing();
    mousehook = NULL;
}

void DestroyHighlighter()
{
    PostQuitMessage(0);
}

LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_NCCREATE:
        m_hwnd = hWnd;
        return DefWindowProc(hWnd, message, wParam, lParam);
    case WM_CREATE:
        // Windows+Shift+H
        RegisterHotKey(m_hwnd, ID_SHORTCUT_ACTIVATE, MOD_WIN | MOD_SHIFT | MOD_NOREPEAT, 0x48);

        // Ctrl+Windows+Shift+H
        RegisterHotKey(m_hwnd, ID_SHORTCUT_EXIT, MOD_CONTROL | MOD_WIN | MOD_SHIFT | MOD_NOREPEAT, 0x48);

        return CreateHighlighter();
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_HOTKEY:
        switch (wParam)
        {
        case ID_SHORTCUT_ACTIVATE:
            if (visible)
            {
                StopDrawing();
            } else
            {
                StartDrawing();
            }
            break;
        case ID_SHORTCUT_EXIT:
            DestroyHighlighter();
            break;
        }
        break;
    case WM_DESTROY:
        DestroyHighlighter();
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }

    return 0;
}

bool MyRegisterClass(HINSTANCE hInstance)
{
    WNDCLASS wc{};

    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.hIcon = LoadIcon(hInstance, IDI_APPLICATION);
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)GetStockObject(NULL_BRUSH);
    wc.lpszClassName = m_className;

    if (!RegisterClassW(&wc))
    {
        return false;
    }

    m_hwndOwner = CreateWindow(L"static", nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hInstance, nullptr);

    DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOREDIRECTIONBITMAP;
    return CreateWindowExW(exStyle, m_className, m_windowTitle, WS_POPUP,
        CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, m_hwndOwner, nullptr, hInstance, nullptr) != nullptr;
}



int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPWSTR    lpCmdLine,
    _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);

    m_hinstance = hInstance;

    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    // Perform application initialization:
    if (!MyRegisterClass(hInstance))
    {
        return FALSE;
    }

    MSG msg;

    // Main message loop:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    return (int)msg.wParam;
}
