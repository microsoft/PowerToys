// InclusiveCrosshairs.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "InclusiveCrosshairs.h"
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

struct InclusiveCrosshairs
{
    bool MyRegisterClass(HINSTANCE hInstance);
    static InclusiveCrosshairs* instance;
    void Terminate();
    void SwitchActivationMode();
    void ApplySettings(InclusiveCrosshairsSettings& settings, bool applyToRuntimeObjects);

private:
    enum class MouseButton
    {
        Left,
        Right
    };

    void DestroyInclusiveCrosshairs();
    static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;
    void StartDrawing();
    void StopDrawing();
    bool CreateInclusiveCrosshairs();
    void UpdateCrosshairsPosition();
    HHOOK m_mouseHook = NULL;
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept;

    static constexpr auto m_className = L"MousePointerCrosshairs";
    static constexpr auto m_windowTitle = L"PowerToys Mouse Pointer Crosshairs";
    static constexpr DWORD AUTO_HIDE_TIMER_ID = 101;
    HWND m_hwndOwner = NULL;
    HWND m_hwnd = NULL;
    HINSTANCE m_hinstance = NULL;
    static constexpr DWORD WM_SWITCH_ACTIVATION_MODE = WM_APP;

    winrt::DispatcherQueueController m_dispatcherQueueController{ nullptr };
    winrt::Compositor m_compositor{ nullptr };
    winrt::Desktop::DesktopWindowTarget m_target{ nullptr };
    winrt::ContainerVisual m_root{ nullptr };
    winrt::LayerVisual m_crosshairs_border_layer{ nullptr };
    winrt::LayerVisual m_crosshairs_layer{ nullptr };
    winrt::SpriteVisual m_left_crosshairs_border{ nullptr };
    winrt::SpriteVisual m_left_crosshairs{ nullptr };
    winrt::SpriteVisual m_right_crosshairs_border{ nullptr };
    winrt::SpriteVisual m_right_crosshairs{ nullptr };
    winrt::SpriteVisual m_top_crosshairs_border{ nullptr };
    winrt::SpriteVisual m_top_crosshairs{ nullptr };
    winrt::SpriteVisual m_bottom_crosshairs_border{ nullptr };
    winrt::SpriteVisual m_bottom_crosshairs{ nullptr };

    bool m_drawing = false;
    bool m_destroyed = false;
    bool m_hiddenCursor = false;
    void SetAutoHideTimer() noexcept;

    // Configurable Settings
    winrt::Windows::UI::Color m_crosshairs_border_color = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_BORDER_COLOR;
    winrt::Windows::UI::Color m_crosshairs_color = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_COLOR;
    int m_crosshairs_radius = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_RADIUS;
    int m_crosshairs_thickness = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_THICKNESS;
    int m_crosshairs_border_size = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_BORDER_SIZE;
    bool m_crosshairs_is_fixed_length_enabled = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_IS_FIXED_LENGTH_ENABLED;
    int m_crosshairs_fixed_length = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_FIXED_LENGTH;
    float m_crosshairs_opacity = max(0.f, min(1.f, (float)INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_OPACITY / 100.0f));
    bool m_crosshairs_auto_hide = INCLUSIVE_MOUSE_DEFAULT_AUTO_HIDE;
};

InclusiveCrosshairs* InclusiveCrosshairs::instance = nullptr;

bool InclusiveCrosshairs::CreateInclusiveCrosshairs()
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
        // \ [crosshairs border layer] LayerVisual
        //   \ [crosshairs border sprites]
        //     [crosshairs layer] LayerVisual
        //     \ [crosshairs sprites]

        m_root = m_compositor.CreateContainerVisual();
        m_root.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_target.Root(m_root);

        m_root.Opacity(m_crosshairs_opacity);

        m_crosshairs_border_layer = m_compositor.CreateLayerVisual();
        m_crosshairs_border_layer.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_root.Children().InsertAtTop(m_crosshairs_border_layer);
        m_crosshairs_border_layer.Opacity(1.0f);

        m_crosshairs_layer = m_compositor.CreateLayerVisual();
        m_crosshairs_layer.RelativeSizeAdjustment({ 1.0f, 1.0f });

        // Create the crosshairs sprites.
        m_left_crosshairs_border = m_compositor.CreateSpriteVisual();
        m_left_crosshairs_border.AnchorPoint({ 1.0f, 0.5f });
        m_left_crosshairs_border.Brush(m_compositor.CreateColorBrush(m_crosshairs_border_color));
        m_crosshairs_border_layer.Children().InsertAtTop(m_left_crosshairs_border);
        m_left_crosshairs = m_compositor.CreateSpriteVisual();
        m_left_crosshairs.AnchorPoint({ 1.0f, 0.5f });
        m_left_crosshairs.Brush(m_compositor.CreateColorBrush(m_crosshairs_color));
        m_crosshairs_layer.Children().InsertAtTop(m_left_crosshairs);

        m_right_crosshairs_border = m_compositor.CreateSpriteVisual();
        m_right_crosshairs_border.AnchorPoint({ 0.0f, 0.5f });
        m_right_crosshairs_border.Brush(m_compositor.CreateColorBrush(m_crosshairs_border_color));
        m_crosshairs_border_layer.Children().InsertAtTop(m_right_crosshairs_border);
        m_right_crosshairs = m_compositor.CreateSpriteVisual();
        m_right_crosshairs.AnchorPoint({ 0.0f, 0.5f });
        m_right_crosshairs.Brush(m_compositor.CreateColorBrush(m_crosshairs_color));
        m_crosshairs_layer.Children().InsertAtTop(m_right_crosshairs);

        m_top_crosshairs_border = m_compositor.CreateSpriteVisual();
        m_top_crosshairs_border.AnchorPoint({ 0.5f, 1.0f });
        m_top_crosshairs_border.Brush(m_compositor.CreateColorBrush(m_crosshairs_border_color));
        m_crosshairs_border_layer.Children().InsertAtTop(m_top_crosshairs_border);
        m_top_crosshairs = m_compositor.CreateSpriteVisual();
        m_top_crosshairs.AnchorPoint({ 0.5f, 1.0f });
        m_top_crosshairs.Brush(m_compositor.CreateColorBrush(m_crosshairs_color));
        m_crosshairs_layer.Children().InsertAtTop(m_top_crosshairs);

        m_bottom_crosshairs_border = m_compositor.CreateSpriteVisual();
        m_bottom_crosshairs_border.AnchorPoint({ 0.5f, 0.0f });
        m_bottom_crosshairs_border.Brush(m_compositor.CreateColorBrush(m_crosshairs_border_color));
        m_crosshairs_border_layer.Children().InsertAtTop(m_bottom_crosshairs_border);
        m_bottom_crosshairs = m_compositor.CreateSpriteVisual();
        m_bottom_crosshairs.AnchorPoint({ 0.5f, 0.0f });
        m_bottom_crosshairs.Brush(m_compositor.CreateColorBrush(m_crosshairs_color));
        m_crosshairs_layer.Children().InsertAtTop(m_bottom_crosshairs);

        m_crosshairs_border_layer.Children().InsertAtTop(m_crosshairs_layer);
        m_crosshairs_layer.Opacity(1.0f);

        UpdateCrosshairsPosition();

        return true;
    }
    catch (...)
    {
        return false;
    }
}

void InclusiveCrosshairs::UpdateCrosshairsPosition()
{
    POINT ptCursor;

    // HACK: Draw with 1 pixel off. Otherwise Windows glitches the task bar transparency when a transparent window fill the whole screen.
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN) + 1, GetSystemMetrics(SM_YVIRTUALSCREEN) + 1, GetSystemMetrics(SM_CXVIRTUALSCREEN) - 2, GetSystemMetrics(SM_CYVIRTUALSCREEN) - 2, 0);

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

    // Crosshair position should receive a minor adjustment for odd values to prevent anti-aliasing due to half pixels, while still looking like it's centered around the mouse pointer.
    float halfPixelAdjustment = m_crosshairs_thickness % 2 == 1 ? 0.5f : 0.0f;
    float borderSizePadding = m_crosshairs_border_size * 2.f;

    {
        float leftCrosshairsFullScreenLength = ptCursor.x - ptMonitorUpperLeft.x - m_crosshairs_radius + halfPixelAdjustment * 2.f;
        float leftCrosshairsLength = m_crosshairs_is_fixed_length_enabled ? m_crosshairs_fixed_length : leftCrosshairsFullScreenLength;
        float leftCrosshairsBorderLength = m_crosshairs_is_fixed_length_enabled ? m_crosshairs_fixed_length + borderSizePadding : leftCrosshairsFullScreenLength + m_crosshairs_border_size;
        m_left_crosshairs_border.Offset({ ptCursor.x - m_crosshairs_radius + m_crosshairs_border_size + halfPixelAdjustment * 2.f, ptCursor.y + halfPixelAdjustment, .0f });
        m_left_crosshairs_border.Size({ leftCrosshairsBorderLength, m_crosshairs_thickness + borderSizePadding });
        m_left_crosshairs.Offset({ ptCursor.x - m_crosshairs_radius + halfPixelAdjustment * 2.f, ptCursor.y + halfPixelAdjustment, .0f });
        m_left_crosshairs.Size({ leftCrosshairsLength, static_cast<float>(m_crosshairs_thickness) });
    }

    {
        float rightCrosshairsFullScreenLength = static_cast<float>(ptMonitorBottomRight.x) - ptCursor.x - m_crosshairs_radius;
        float rightCrosshairsLength = m_crosshairs_is_fixed_length_enabled ? m_crosshairs_fixed_length : rightCrosshairsFullScreenLength;
        float rightCrosshairsBorderLength = m_crosshairs_is_fixed_length_enabled ? m_crosshairs_fixed_length + borderSizePadding : rightCrosshairsFullScreenLength + m_crosshairs_border_size;
        m_right_crosshairs_border.Offset({ static_cast<float>(ptCursor.x) + m_crosshairs_radius - m_crosshairs_border_size, ptCursor.y + halfPixelAdjustment, .0f });
        m_right_crosshairs_border.Size({ rightCrosshairsBorderLength, m_crosshairs_thickness + borderSizePadding });
        m_right_crosshairs.Offset({ static_cast<float>(ptCursor.x) + m_crosshairs_radius, ptCursor.y + halfPixelAdjustment, .0f });
        m_right_crosshairs.Size({ rightCrosshairsLength, static_cast<float>(m_crosshairs_thickness) });
    }

    {
        float topCrosshairsFullScreenLength = ptCursor.y - ptMonitorUpperLeft.y - m_crosshairs_radius + halfPixelAdjustment * 2.f;
        float topCrosshairsLength = m_crosshairs_is_fixed_length_enabled ? m_crosshairs_fixed_length : topCrosshairsFullScreenLength;
        float topCrosshairsBorderLength = m_crosshairs_is_fixed_length_enabled ? m_crosshairs_fixed_length + borderSizePadding : topCrosshairsFullScreenLength + m_crosshairs_border_size;
        m_top_crosshairs_border.Offset({ ptCursor.x + halfPixelAdjustment, ptCursor.y - m_crosshairs_radius + m_crosshairs_border_size + halfPixelAdjustment * 2.f, .0f });
        m_top_crosshairs_border.Size({ m_crosshairs_thickness + borderSizePadding, topCrosshairsBorderLength });
        m_top_crosshairs.Offset({ ptCursor.x + halfPixelAdjustment, ptCursor.y - m_crosshairs_radius + halfPixelAdjustment * 2.f, .0f });
        m_top_crosshairs.Size({ static_cast<float>(m_crosshairs_thickness), topCrosshairsLength });
    }

    {
        float bottomCrosshairsFullScreenLength = static_cast<float>(ptMonitorBottomRight.y) - ptCursor.y - m_crosshairs_radius;
        float bottomCrosshairsLength = m_crosshairs_is_fixed_length_enabled ? m_crosshairs_fixed_length : bottomCrosshairsFullScreenLength;
        float bottomCrosshairsBorderLength = m_crosshairs_is_fixed_length_enabled ? m_crosshairs_fixed_length + borderSizePadding : bottomCrosshairsFullScreenLength + m_crosshairs_border_size;
        m_bottom_crosshairs_border.Offset({ ptCursor.x + halfPixelAdjustment, static_cast<float>(ptCursor.y) + m_crosshairs_radius - m_crosshairs_border_size, .0f });
        m_bottom_crosshairs_border.Size({ m_crosshairs_thickness + borderSizePadding, bottomCrosshairsBorderLength });
        m_bottom_crosshairs.Offset({ ptCursor.x + halfPixelAdjustment, static_cast<float>(ptCursor.y) + m_crosshairs_radius, .0f });
        m_bottom_crosshairs.Size({ static_cast<float>(m_crosshairs_thickness), bottomCrosshairsLength });
    }
}

LRESULT CALLBACK InclusiveCrosshairs::MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept
{
    if (nCode >= 0)
    {
        MSLLHOOKSTRUCT* hookData = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
        if (wParam == WM_MOUSEMOVE)
        {
            instance->UpdateCrosshairsPosition();
        }
    }
    return CallNextHookEx(0, nCode, wParam, lParam);
}

void InclusiveCrosshairs::StartDrawing()
{
    Logger::info("Start drawing crosshairs.");
    Trace::StartDrawingCrosshairs();
    UpdateCrosshairsPosition();

    m_hiddenCursor = false;
    if (m_crosshairs_auto_hide)
    {
        CURSORINFO cursorInfo{};
        cursorInfo.cbSize = sizeof(cursorInfo);
        if (GetCursorInfo(&cursorInfo))
        {
            m_hiddenCursor = !(cursorInfo.flags & CURSOR_SHOWING);
        }

        SetAutoHideTimer();
    }

    if (!m_hiddenCursor)
    {
        ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    }

    m_drawing = true;
    m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, m_hinstance, 0);
}

void InclusiveCrosshairs::StopDrawing()
{
    Logger::info("Stop drawing crosshairs.");
    m_drawing = false;
    ShowWindow(m_hwnd, SW_HIDE);
    UnhookWindowsHookEx(m_mouseHook);
    m_mouseHook = NULL;
    KillTimer(m_hwnd, AUTO_HIDE_TIMER_ID);
}

void InclusiveCrosshairs::SwitchActivationMode()
{
    PostMessage(m_hwnd, WM_SWITCH_ACTIVATION_MODE, 0, 0);
}

void InclusiveCrosshairs::ApplySettings(InclusiveCrosshairsSettings& settings, bool applyToRunTimeObjects)
{
    m_crosshairs_radius = settings.crosshairsRadius;
    m_crosshairs_thickness = settings.crosshairsThickness;
    m_crosshairs_color = settings.crosshairsColor;
    m_crosshairs_opacity = max(0.f, min(1.f, (float)settings.crosshairsOpacity / 100.0f));
    m_crosshairs_border_color = settings.crosshairsBorderColor;
    m_crosshairs_border_size = settings.crosshairsBorderSize;
    bool autoHideChanged = m_crosshairs_auto_hide != settings.crosshairsAutoHide;
    m_crosshairs_auto_hide = settings.crosshairsAutoHide;
    m_crosshairs_is_fixed_length_enabled = settings.crosshairsIsFixedLengthEnabled;
    m_crosshairs_fixed_length = settings.crosshairsFixedLength;

    if (applyToRunTimeObjects)
    {
        if (autoHideChanged)
        {
            if (m_crosshairs_auto_hide)
            {
                SetAutoHideTimer();
            }
            else
            {
                KillTimer(m_hwnd, AUTO_HIDE_TIMER_ID);

                // Edge case of settings being changed with hidden crosshairs: timer time-out is 1 seconds
                if (m_drawing && m_hiddenCursor)
                {
                    instance->m_hiddenCursor = false;
                    ShowWindow(instance->m_hwnd, SW_SHOWNOACTIVATE);
                }
            }
        }

        // Runtime objects already created. Should update in the owner thread.
        if (m_dispatcherQueueController == nullptr)
        {
            Logger::warn("Tried accessing the dispatch queue controller before it was initialized.");
            // No dispatcher Queue Controller? Means initialization still hasn't run, so settings will be applied then.
            return;
        }
        auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
        InclusiveCrosshairsSettings localSettings = settings;
        bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
            if (!m_destroyed)
            {
                // Apply new settings to runtime composition objects.
                m_left_crosshairs.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshairs_color);
                m_right_crosshairs.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshairs_color);
                m_top_crosshairs.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshairs_color);
                m_bottom_crosshairs.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshairs_color);
                m_left_crosshairs_border.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshairs_border_color);
                m_right_crosshairs_border.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshairs_border_color);
                m_top_crosshairs_border.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshairs_border_color);
                m_bottom_crosshairs_border.Brush().as<winrt::CompositionColorBrush>().Color(m_crosshairs_border_color);
                m_root.Opacity(m_crosshairs_opacity);
                UpdateCrosshairsPosition();
            }
        });
        if (!enqueueSucceeded)
        {
            Logger::error("Couldn't enqueue message to update the crosshairs settings.");
        }
    }
}

void InclusiveCrosshairs::DestroyInclusiveCrosshairs()
{
    StopDrawing();
    PostQuitMessage(0);
}

LRESULT CALLBACK InclusiveCrosshairs::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    switch (message)
    {
    case WM_NCCREATE:
        instance->m_hwnd = hWnd;
        return DefWindowProc(hWnd, message, wParam, lParam);
    case WM_CREATE:
        return instance->CreateInclusiveCrosshairs() ? 0 : -1;
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_SWITCH_ACTIVATION_MODE:
        if (instance->m_drawing)
        {
            instance->StopDrawing();
        }
        else
        {
            instance->StartDrawing();
        }
        break;
    case WM_DESTROY:
        instance->DestroyInclusiveCrosshairs();
        break;
    case WM_TIMER:
        if (wParam == AUTO_HIDE_TIMER_ID && instance->m_drawing)
        {
            CURSORINFO cursorInfo{};
            cursorInfo.cbSize = sizeof(cursorInfo);
            if (GetCursorInfo(&cursorInfo))
            {
                if (cursorInfo.flags & CURSOR_SHOWING)
                {
                    if (instance->m_hiddenCursor)
                    {
                        instance->m_hiddenCursor = false;
                        ShowWindow(instance->m_hwnd, SW_SHOWNOACTIVATE);
                    }
                }
                else
                {
                    if (!instance->m_hiddenCursor)
                    {
                        instance->m_hiddenCursor = true;
                        ShowWindow(instance->m_hwnd, SW_HIDE);
                    }
                }
            }
        }
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

bool InclusiveCrosshairs::MyRegisterClass(HINSTANCE hInstance)
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
        wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(NULL_BRUSH));
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

void InclusiveCrosshairs::Terminate()
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

void InclusiveCrosshairs::SetAutoHideTimer() noexcept
{
    if (SetTimer(m_hwnd, AUTO_HIDE_TIMER_ID, 1000, NULL) == 0)
    {
        int error = GetLastError();
        Logger::trace("Failed to create auto hide timer. Last error: {}", error);
    }
}

#pragma region InclusiveCrosshairs_API

void InclusiveCrosshairsApplySettings(InclusiveCrosshairsSettings& settings)
{
    if (InclusiveCrosshairs::instance != nullptr)
    {
        Logger::info("Applying settings.");
        InclusiveCrosshairs::instance->ApplySettings(settings, true);
    }
}

void InclusiveCrosshairsSwitch()
{
    if (InclusiveCrosshairs::instance != nullptr)
    {
        Logger::info("Switching activation mode.");
        InclusiveCrosshairs::instance->SwitchActivationMode();
    }
}

void InclusiveCrosshairsDisable()
{
    if (InclusiveCrosshairs::instance != nullptr)
    {
        Logger::info("Terminating the crosshairs instance.");
        InclusiveCrosshairs::instance->Terminate();
    }
}

bool InclusiveCrosshairsIsEnabled()
{
    return (InclusiveCrosshairs::instance != nullptr);
}

int InclusiveCrosshairsMain(HINSTANCE hInstance, InclusiveCrosshairsSettings& settings)
{
    Logger::info("Starting a crosshairs instance.");
    if (InclusiveCrosshairs::instance != nullptr)
    {
        Logger::error("A crosshairs instance was still working when trying to start a new one.");
        return 0;
    }

    // Perform application initialization:
    InclusiveCrosshairs crosshairs;
    InclusiveCrosshairs::instance = &crosshairs;
    crosshairs.ApplySettings(settings, false);
    if (!crosshairs.MyRegisterClass(hInstance))
    {
        Logger::error("Couldn't initialize a crosshairs instance.");
        InclusiveCrosshairs::instance = nullptr;
        return FALSE;
    }
    Logger::info("Initialized the crosshairs instance.");

    if (settings.autoActivate)
    {
        crosshairs.SwitchActivationMode();
    }

    MSG msg;

    // Main message loop:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    Logger::info("Crosshairs message loop ended.");
    InclusiveCrosshairs::instance = nullptr;

    return (int)msg.wParam;
}

#pragma endregion InclusiveCrosshairs_API
