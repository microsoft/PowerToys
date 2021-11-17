// MouseHighlighter.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "MouseHighlighter.h"
#include "trace.h"

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

struct Highlighter
{
    bool MyRegisterClass(HINSTANCE hInstance);
    static Highlighter* instance;
    void Terminate();
    void SwitchActivationMode();
    void ApplySettings(MouseHighlighterSettings settings);

private:
    void DestroyHighlighter();
    static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;
    void StartDrawing();
    void StopDrawing();
    bool CreateHighlighter();
    void AddDrawingPoint(winrt::Windows::UI::Color color);
    void ClearDrawing();
    HHOOK m_mouseHook = NULL;
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept;

    static constexpr auto m_className = L"MouseHighlighter";
    static constexpr auto m_windowTitle = L"MouseHighlighter";
    HWND m_hwndOwner = NULL;
    HWND m_hwnd = NULL;
    HINSTANCE m_hinstance = NULL;
    static constexpr DWORD WM_SWITCH_ACTIVATION_MODE = WM_APP;

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
    float m_radius = MOUSE_HIGHLIGHTER_DEFAULT_RADIUS;

    int m_fadeDelay_ms = MOUSE_HIGHLIGHTER_DEFAULT_DELAY_MS;
    int m_fadeDuration_ms = MOUSE_HIGHLIGHTER_DEFAULT_DURATION_MS;

    winrt::Windows::UI::Color m_leftClickColor = MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR;
    winrt::Windows::UI::Color m_rightClickColor = MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR;
};

Highlighter* Highlighter::instance = nullptr;

bool Highlighter::CreateHighlighter()
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

        // Create the shapes container visual and add it to root.
        m_shape = m_compositor.CreateShapeVisual();
        m_shape.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_root.Children().InsertAtTop(m_shape);

        return true;
    } catch (...)
    {
        return false;
    }
}


void Highlighter::AddDrawingPoint(winrt::Windows::UI::Color color)
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

void Highlighter::ClearDrawing()
{
    m_shape.Shapes().Clear();
}

LRESULT CALLBACK Highlighter::MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept
{
    if (nCode >= 0)
    {
        MSLLHOOKSTRUCT* hookData = (MSLLHOOKSTRUCT*)lParam;
        if (wParam == WM_LBUTTONDOWN)
        {
            instance->AddDrawingPoint(instance->m_leftClickColor);
        }
        if (wParam == WM_RBUTTONDOWN)
        {
            instance->AddDrawingPoint(instance->m_rightClickColor);
        }
    }
    return CallNextHookEx(0, nCode, wParam, lParam);
}


void Highlighter::StartDrawing()
{
    Logger::info("Starting draw mode.");
    Trace::StartHighlightingSession();
    visible = true;
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
        GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN), 0);
    ClearDrawing();
    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, m_hinstance, 0);
}

void Highlighter::StopDrawing()
{
    Logger::info("Stopping draw mode.");
    visible = false;
    ShowWindow(m_hwnd, SW_HIDE);
    UnhookWindowsHookEx(m_mouseHook);
    ClearDrawing();
    m_mouseHook = NULL;
}

void Highlighter::SwitchActivationMode()
{
    PostMessage(m_hwnd, WM_SWITCH_ACTIVATION_MODE, 0, 0);
}

void Highlighter::ApplySettings(MouseHighlighterSettings settings) {
    m_radius = (float)settings.radius;
    m_fadeDelay_ms = settings.fadeDelayMs;
    m_fadeDuration_ms = settings.fadeDurationMs;
    m_leftClickColor = settings.leftButtonColor;
    m_rightClickColor = settings.rightButtonColor;
}

void Highlighter::DestroyHighlighter()
{
    StopDrawing();
    PostQuitMessage(0);
}

LRESULT CALLBACK Highlighter::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    switch (message)
    {
    case WM_NCCREATE:
        instance->m_hwnd = hWnd;
        return DefWindowProc(hWnd, message, wParam, lParam);
    case WM_CREATE:
        return instance->CreateHighlighter() ? 0 : -1;
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_SWITCH_ACTIVATION_MODE:
        if (instance->visible)
        {
            instance->StopDrawing();
        }
        else
        {
            instance->StartDrawing();
        }
        break;
    case WM_DESTROY:
        instance->DestroyHighlighter();
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

bool Highlighter::MyRegisterClass(HINSTANCE hInstance)
{
    WNDCLASS wc{};

    m_hinstance = hInstance;

    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    if (!GetClassInfoW(hInstance, m_className, &wc))
    {
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
    }

    m_hwndOwner = CreateWindow(L"static", nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hInstance, nullptr);

    DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW;
    return CreateWindowExW(exStyle, m_className, m_windowTitle, WS_POPUP,
        CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, m_hwndOwner, nullptr, hInstance, nullptr) != nullptr;
}

void Highlighter::Terminate()
{
    auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
    bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
        DestroyWindow(m_hwndOwner);
    });
    if (!enqueueSucceeded)
    {
        Logger::error("Couldn't enqueue message to destroy the window.");
    }
}

#pragma region MouseHighlighter_API

void MouseHighlighterApplySettings(MouseHighlighterSettings settings)
{
    if (Highlighter::instance != nullptr)
    {
        Logger::info("Applying settings.");
        Highlighter::instance->ApplySettings(settings);
    }
}

void MouseHighlighterSwitch()
{
    if (Highlighter::instance != nullptr)
    {
        Logger::info("Switching activation mode.");
        Highlighter::instance->SwitchActivationMode();
    }
}

void MouseHighlighterDisable()
{
    if (Highlighter::instance != nullptr)
    {
        Logger::info("Terminating the highlighter instance.");
        Highlighter::instance->Terminate();
    }
}

bool MouseHighlighterIsEnabled()
{
    return (Highlighter::instance != nullptr);
}

int MouseHighlighterMain(HINSTANCE hInstance, MouseHighlighterSettings settings)
{
    Logger::info("Starting a highlighter instance.");
    if (Highlighter::instance != nullptr)
    {
        Logger::error("A highlighter instance was still working when trying to start a new one.");
        return 0;
    }

    // Perform application initialization:
    Highlighter highlighter;
    Highlighter::instance = &highlighter;
    highlighter.ApplySettings(settings);
    if (!highlighter.MyRegisterClass(hInstance))
    {
        Logger::error("Couldn't initialize a highlighter instance.");
        Highlighter::instance = nullptr;
        return FALSE;
    }
    Logger::info("Initialized the highlighter instance.");

    MSG msg;

    // Main message loop:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    Logger::info("Mouse highlighter message loop ended.");
    Highlighter::instance = nullptr;

    return (int)msg.wParam;
}

#pragma endregion MouseHighlighter_API
