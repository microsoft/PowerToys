// InclusiveCrosshair.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "InclusiveCrosshair.h"
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

struct InclusiveCrosshair
{
    bool MyRegisterClass(HINSTANCE hInstance);
    static InclusiveCrosshair* instance;
    void Terminate();
    void SwitchActivationMode();
    void ApplySettings(InclusiveCrosshairSettings& settings, bool applyToRuntimeObjects);

private:
    enum class MouseButton
    {
        Left,
        Right
    };

    void DestroyInclusiveCrosshair();
    static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;
    void StartDrawing();
    void StopDrawing();
    bool CreateInclusiveCrosshair();
    void UpdateCrosshairPosition();
    HHOOK m_mouseHook = NULL;
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept;

    static constexpr auto m_className = L"MousePointerCrosshair";
    static constexpr auto m_windowTitle = L"PowerToys Mouse Pointer Crosshair";
    HWND m_hwndOwner = NULL;
    HWND m_hwnd = NULL;
    HINSTANCE m_hinstance = NULL;
    static constexpr DWORD WM_SWITCH_ACTIVATION_MODE = WM_APP;

    winrt::DispatcherQueueController m_dispatcherQueueController{ nullptr };
    winrt::Compositor m_compositor{ nullptr };
    winrt::Desktop::DesktopWindowTarget m_target{ nullptr };
    winrt::ContainerVisual m_root{ nullptr };
    winrt::LayerVisual m_crosshair_border_layer{ nullptr };
    winrt::LayerVisual m_crosshair_layer{ nullptr };
    winrt::SpriteVisual m_left_crosshair_border{ nullptr };
    winrt::SpriteVisual m_left_crosshair{ nullptr };
    winrt::SpriteVisual m_right_crosshair_border{ nullptr };
    winrt::SpriteVisual m_right_crosshair{ nullptr };
    winrt::SpriteVisual m_top_crosshair_border{ nullptr };
    winrt::SpriteVisual m_top_crosshair{ nullptr };
    winrt::SpriteVisual m_bottom_crosshair_border{ nullptr };
    winrt::SpriteVisual m_bottom_crosshair{ nullptr };

    bool m_visible = false;
    bool m_destroyed = false;

    // Configurable Settings
    winrt::Windows::UI::Color m_crosshair_border_color = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_BORDER_COLOR;
    winrt::Windows::UI::Color m_crosshair_color = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_COLOR;
    float m_crosshair_radius = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_RADIUS;
    float m_crosshair_thickness = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_THICKNESS;
    float m_crosshair_border_size = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_BORDER_SIZE;
    float m_crosshair_opacity = max(0.f, min(1.f, (float)INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_OPACITY / 100.0f));
};

InclusiveCrosshair* InclusiveCrosshair::instance = nullptr;

bool InclusiveCrosshair::CreateInclusiveCrosshair()
{
    try
    {
        // We need a dispatcher queue.
        DispatcherQueueOptions options = {
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

        // Our composition tree:
        //
        // [root] ContainerVisual
        // \ [crosshair border layer] LayerVisual
        //   \ [crosshair border sprites]
        //     [crosshair layer] LayerVisual
        //     \ [crosshair sprites]

        m_root = m_compositor.CreateContainerVisual();
        m_root.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_target.Root(m_root);

        m_root.Opacity(m_crosshair_opacity);

        m_crosshair_border_layer = m_compositor.CreateLayerVisual();
        m_crosshair_border_layer.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_root.Children().InsertAtTop(m_crosshair_border_layer);
        m_crosshair_border_layer.Opacity(1.0f);

        m_crosshair_layer = m_compositor.CreateLayerVisual();
        m_crosshair_layer.RelativeSizeAdjustment({ 1.0f, 1.0f });

        // Create the crosshair sprites.
        m_left_crosshair_border = m_compositor.CreateSpriteVisual();
        m_left_crosshair_border.AnchorPoint({ 1.0f, 0.5f });
        m_left_crosshair_border.Brush(m_compositor.CreateColorBrush(m_crosshair_border_color));
        m_crosshair_border_layer.Children().InsertAtTop(m_left_crosshair_border);
        m_left_crosshair = m_compositor.CreateSpriteVisual();
        m_left_crosshair.AnchorPoint({ 1.0f, 0.5f });
        m_left_crosshair.Brush(m_compositor.CreateColorBrush(m_crosshair_color));
        m_crosshair_layer.Children().InsertAtTop(m_left_crosshair);

        m_right_crosshair_border = m_compositor.CreateSpriteVisual();
        m_right_crosshair_border.AnchorPoint({ 0.0f, 0.5f });
        m_right_crosshair_border.Brush(m_compositor.CreateColorBrush(m_crosshair_border_color));
        m_crosshair_border_layer.Children().InsertAtTop(m_right_crosshair_border);
        m_right_crosshair = m_compositor.CreateSpriteVisual();
        m_right_crosshair.AnchorPoint({ 0.0f, 0.5f });
        m_right_crosshair.Brush(m_compositor.CreateColorBrush(m_crosshair_color));
        m_crosshair_layer.Children().InsertAtTop(m_right_crosshair);

        m_top_crosshair_border = m_compositor.CreateSpriteVisual();
        m_top_crosshair_border.AnchorPoint({ 0.5f, 1.0f });
        m_top_crosshair_border.Brush(m_compositor.CreateColorBrush(m_crosshair_border_color));
        m_crosshair_border_layer.Children().InsertAtTop(m_top_crosshair_border);
        m_top_crosshair = m_compositor.CreateSpriteVisual();
        m_top_crosshair.AnchorPoint({ 0.5f, 1.0f });
        m_top_crosshair.Brush(m_compositor.CreateColorBrush(m_crosshair_color));
        m_crosshair_layer.Children().InsertAtTop(m_top_crosshair);

        m_bottom_crosshair_border = m_compositor.CreateSpriteVisual();
        m_bottom_crosshair_border.AnchorPoint({ 0.5f, 0.0f });
        m_bottom_crosshair_border.Brush(m_compositor.CreateColorBrush(m_crosshair_border_color));
        m_crosshair_border_layer.Children().InsertAtTop(m_bottom_crosshair_border);
        m_bottom_crosshair = m_compositor.CreateSpriteVisual();
        m_bottom_crosshair.AnchorPoint({ 0.5f, 0.0f });
        m_bottom_crosshair.Brush(m_compositor.CreateColorBrush(m_crosshair_color));
        m_crosshair_layer.Children().InsertAtTop(m_bottom_crosshair);

        m_crosshair_border_layer.Children().InsertAtTop(m_crosshair_layer);
        m_crosshair_layer.Opacity(1.0f);

        UpdateCrosshairPosition();

        return true;
    }
    catch (...)
    {
        return false;
    }
}

void InclusiveCrosshair::UpdateCrosshairPosition()
{
    POINT ptCursor;

    GetCursorPos(&ptCursor);

    HMONITOR cursorMonitor = MonitorFromPoint(ptCursor, MONITOR_DEFAULTTONEAREST);

    if (cursorMonitor == NULL)
    {
        return;
    }

    MONITORINFO monitorInfo;
    monitorInfo.cbSize = sizeof(monitorInfo);

    if (!GetMonitorInfo(cursorMonitor, &monitorInfo))
    {
        return;
    }

    POINT ptMonitorUpperLeft;
    ptMonitorUpperLeft.x = monitorInfo.rcMonitor.left;
    ptMonitorUpperLeft.y = monitorInfo.rcMonitor.top;

    POINT ptMonitorBottomRight;
    ptMonitorBottomRight.x = monitorInfo.rcMonitor.right;
    ptMonitorBottomRight.y = monitorInfo.rcMonitor.bottom;

    // Convert everything to client coordinates.
    ScreenToClient(m_hwnd, &ptCursor);
    ScreenToClient(m_hwnd, &ptMonitorUpperLeft);
    ScreenToClient(m_hwnd, &ptMonitorBottomRight);

    // Position crosshair components around the mouse pointer.
    m_left_crosshair_border.Offset({ (float)ptCursor.x - m_crosshair_radius + m_crosshair_border_size, (float)ptCursor.y, .0f });
    m_left_crosshair_border.Size({ (float)ptCursor.x - (float)ptMonitorUpperLeft.x - m_crosshair_radius + m_crosshair_border_size, m_crosshair_thickness + m_crosshair_border_size * 2 });
    m_left_crosshair.Offset({ (float)ptCursor.x - m_crosshair_radius, (float)ptCursor.y, .0f });
    m_left_crosshair.Size({ (float)ptCursor.x - (float)ptMonitorUpperLeft.x - m_crosshair_radius, m_crosshair_thickness });

    m_right_crosshair_border.Offset({ (float)ptCursor.x + m_crosshair_radius - m_crosshair_border_size, (float)ptCursor.y, .0f });
    m_right_crosshair_border.Size({ (float)ptMonitorBottomRight.x - (float)ptCursor.x - m_crosshair_radius + m_crosshair_border_size, m_crosshair_thickness + m_crosshair_border_size * 2 });
    m_right_crosshair.Offset({ (float)ptCursor.x + m_crosshair_radius, (float)ptCursor.y, .0f });
    m_right_crosshair.Size({ (float)ptMonitorBottomRight.x - (float)ptCursor.x - m_crosshair_radius, m_crosshair_thickness });

    m_top_crosshair_border.Offset({ (float)ptCursor.x, (float)ptCursor.y - m_crosshair_radius + m_crosshair_border_size, .0f });
    m_top_crosshair_border.Size({ m_crosshair_thickness + m_crosshair_border_size * 2, (float)ptCursor.y - (float)ptMonitorUpperLeft.y - m_crosshair_radius + m_crosshair_border_size });
    m_top_crosshair.Offset({ (float)ptCursor.x, (float)ptCursor.y - m_crosshair_radius, .0f });
    m_top_crosshair.Size({ m_crosshair_thickness, (float)ptCursor.y - (float)ptMonitorUpperLeft.y - m_crosshair_radius });

    m_bottom_crosshair_border.Offset({ (float)ptCursor.x, (float)ptCursor.y + m_crosshair_radius - m_crosshair_border_size, .0f });
    m_bottom_crosshair_border.Size({ m_crosshair_thickness + m_crosshair_border_size * 2, (float)ptMonitorBottomRight.y - (float)ptCursor.y - m_crosshair_radius + m_crosshair_border_size });
    m_bottom_crosshair.Offset({ (float)ptCursor.x, (float)ptCursor.y + m_crosshair_radius, .0f });
    m_bottom_crosshair.Size({ m_crosshair_thickness, (float)ptMonitorBottomRight.y - (float)ptCursor.y - m_crosshair_radius });

}

LRESULT CALLBACK InclusiveCrosshair::MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept
{
    if (nCode >= 0)
    {
        MSLLHOOKSTRUCT* hookData = (MSLLHOOKSTRUCT*)lParam;
        if (wParam == WM_MOUSEMOVE) {
            instance->UpdateCrosshairPosition();
        }
    }
    return CallNextHookEx(0, nCode, wParam, lParam);
}

void InclusiveCrosshair::StartDrawing()
{
    Logger::info("Start drawing crosshairs.");
    Trace::StartDrawingCrosshair();
    m_visible = true;
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN), GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN), 0);
    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, m_hinstance, 0);
    UpdateCrosshairPosition();
}

void InclusiveCrosshair::StopDrawing()
{
    Logger::info("Stop drawing crosshairs.");
    m_visible = false;
    ShowWindow(m_hwnd, SW_HIDE);
    UnhookWindowsHookEx(m_mouseHook);
    m_mouseHook = NULL;
}

void InclusiveCrosshair::SwitchActivationMode()
{
    PostMessage(m_hwnd, WM_SWITCH_ACTIVATION_MODE, 0, 0);
}

void InclusiveCrosshair::ApplySettings(InclusiveCrosshairSettings& settings, bool applyToRunTimeObjects)
{
    m_crosshair_radius = (float)settings.crosshairRadius;
    m_crosshair_thickness = (float)settings.crosshairThickness;
    m_crosshair_color = settings.crosshairColor;
    m_crosshair_opacity = max(0.f, min(1.f, (float)settings.crosshairOpacity / 100.0f));
    m_crosshair_border_color = settings.crosshairBorderColor;
    m_crosshair_border_size = (float)settings.crosshairBorderSize;

    if (applyToRunTimeObjects)
    {
        // Runtime objects already created. Should update in the owner thread.
        auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
        InclusiveCrosshairSettings localSettings = settings;
        bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
            if (!m_destroyed)
            {
                // Apply new settings to runtime composition objects.
                m_left_crosshair.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshair_color);
                m_right_crosshair.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshair_color);
                m_top_crosshair.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshair_color);
                m_bottom_crosshair.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshair_color);
                m_left_crosshair_border.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshair_border_color);
                m_right_crosshair_border.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshair_border_color);
                m_top_crosshair_border.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshair_border_color);
                m_bottom_crosshair_border.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshair_border_color);
                m_root.Opacity(m_crosshair_opacity);
                UpdateCrosshairPosition();
            }
        });
        if (!enqueueSucceeded)
        {
            Logger::error("Couldn't enqueue message to update the crosshair settings.");
        }
    }
}

void InclusiveCrosshair::DestroyInclusiveCrosshair()
{
    StopDrawing();
    PostQuitMessage(0);
}

LRESULT CALLBACK InclusiveCrosshair::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    switch (message)
    {
    case WM_NCCREATE:
        instance->m_hwnd = hWnd;
        return DefWindowProc(hWnd, message, wParam, lParam);
    case WM_CREATE:
        return instance->CreateInclusiveCrosshair() ? 0 : -1;
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_SWITCH_ACTIVATION_MODE:
        if (instance->m_visible)
        {
            instance->StopDrawing();
        }
        else
        {
            instance->StartDrawing();
        }
        break;
    case WM_DESTROY:
        instance->DestroyInclusiveCrosshair();
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

bool InclusiveCrosshair::MyRegisterClass(HINSTANCE hInstance)
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
    return CreateWindowExW(exStyle, m_className, m_windowTitle, WS_POPUP, CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, m_hwndOwner, nullptr, hInstance, nullptr) != nullptr;
}

void InclusiveCrosshair::Terminate()
{
    auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
    bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
        m_destroyed = true;
        DestroyWindow(m_hwndOwner);
    });
    if (!enqueueSucceeded)
    {
        Logger::error("Couldn't enqueue message to destroy the window.");
    }
}

#pragma region InclusiveCrosshair_API

void InclusiveCrosshairApplySettings(InclusiveCrosshairSettings& settings)
{
    if (InclusiveCrosshair::instance != nullptr)
    {
        Logger::info("Applying settings.");
        InclusiveCrosshair::instance->ApplySettings(settings, true);
    }
}

void InclusiveCrosshairSwitch()
{
    if (InclusiveCrosshair::instance != nullptr)
    {
        Logger::info("Switching activation mode.");
        InclusiveCrosshair::instance->SwitchActivationMode();
    }
}

void InclusiveCrosshairDisable()
{
    if (InclusiveCrosshair::instance != nullptr)
    {
        Logger::info("Terminating the crosshair instance.");
        InclusiveCrosshair::instance->Terminate();
    }
}

bool InclusiveCrosshairIsEnabled()
{
    return (InclusiveCrosshair::instance != nullptr);
}

int InclusiveCrosshairMain(HINSTANCE hInstance, InclusiveCrosshairSettings& settings)
{
    Logger::info("Starting a crosshair instance.");
    if (InclusiveCrosshair::instance != nullptr)
    {
        Logger::error("A crosshair instance was still working when trying to start a new one.");
        return 0;
    }

    // Perform application initialization:
    InclusiveCrosshair crosshair;
    InclusiveCrosshair::instance = &crosshair;
    crosshair.ApplySettings(settings, false);
    if (!crosshair.MyRegisterClass(hInstance))
    {
        Logger::error("Couldn't initialize a crosshair instance.");
        InclusiveCrosshair::instance = nullptr;
        return FALSE;
    }
    Logger::info("Initialized the crosshair instance.");

    MSG msg;

    // Main message loop:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    Logger::info("Crosshair message loop ended.");
    InclusiveCrosshair::instance = nullptr;

    return (int)msg.wParam;
}

#pragma endregion InclusiveCrosshair_API
