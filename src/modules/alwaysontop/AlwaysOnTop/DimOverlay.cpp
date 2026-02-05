#include "pch.h"
#include "DimOverlay.h"

#include <common/utils/MsWindowsSettings.h>

#include <DispatcherQueue.h>
#include <dwmapi.h>
#include <windows.ui.composition.interop.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.h>

#include <chrono>
#include <algorithm>
#include <cmath>

namespace winrt
{
    using namespace winrt::Windows::System;
    using namespace winrt::Windows::UI::Composition;
    using namespace winrt::Windows::UI::Composition::Desktop;
}

namespace ABI
{
    using namespace ABI::Windows::System;
    using namespace ABI::Windows::UI::Composition::Desktop;
}

namespace
{
    constexpr wchar_t OverlayWindowClassName[] = L"AlwaysOnTop_DimOverlay";
    constexpr int FadeDurationMs = 200;

    winrt::Windows::UI::Color GetDefaultDimColor()
    {
        return winrt::Windows::UI::ColorHelper::FromArgb(160, 32, 32, 32);
    }
}

DimOverlay::~DimOverlay()
{
    Terminate();
}

bool DimOverlay::Initialize(HINSTANCE hinstance)
{
    m_hinstance = hinstance;
    return CreateWindowAndVisuals();
}

void DimOverlay::Terminate()
{
    if (m_destroyed)
    {
        return;
    }

    m_destroyed = true;

    if (m_hwnd)
    {
        DestroyWindow(m_hwnd);
        m_hwnd = nullptr;
    }

    if (m_hwndOwner)
    {
        DestroyWindow(m_hwndOwner);
        m_hwndOwner = nullptr;
    }
}

HWND DimOverlay::Hwnd() const noexcept
{
    return m_hwnd;
}

void DimOverlay::Update(std::vector<DimOverlayHole> holes, bool visible)
{
    if (m_destroyed || !m_dispatcherQueueController)
    {
        return;
    }

    auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
    bool enqueueSucceeded = dispatcherQueue.TryEnqueue([this, holes = std::move(holes), visible]() {
        if (m_destroyed || !m_hwnd)
        {
            return;
        }

        m_lastHoles = holes;
        UpdateWindowBounds();
        UpdateRegion(holes);
        SetVisible(visible);
    });

    if (!enqueueSucceeded)
    {
        Logger::error("Couldn't enqueue message to update the dim overlay.");
    }
}

LRESULT CALLBACK DimOverlay::WndProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<DimOverlay*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    if ((thisRef == nullptr) && (message == WM_CREATE))
    {
        const auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = static_cast<DimOverlay*>(createStruct->lpCreateParams);
        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }

    return thisRef ? thisRef->MessageHandler(hwnd, message, wparam, lparam) : DefWindowProc(hwnd, message, wparam, lparam);
}

LRESULT DimOverlay::MessageHandler(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_DISPLAYCHANGE:
        UpdateWindowBounds();
        UpdateRegion(m_lastHoles);
        break;
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_ERASEBKGND:
        return TRUE;
    case WM_NCDESTROY:
        SetWindowLongPtr(hwnd, GWLP_USERDATA, 0);
        break;
    default:
        break;
    }

    return DefWindowProc(hwnd, message, wparam, lparam);
}

bool DimOverlay::CreateWindowAndVisuals()
{
    WNDCLASS wc{};
    if (!GetClassInfoW(m_hinstance, OverlayWindowClassName, &wc))
    {
        wc.lpfnWndProc = DimOverlay::WndProc;
        wc.hInstance = m_hinstance;
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(NULL_BRUSH));
        wc.lpszClassName = OverlayWindowClassName;

        if (!RegisterClassW(&wc))
        {
            Logger::error("Failed to register DimOverlay window class. GetLastError={}", GetLastError());
            return false;
        }
    }

    m_hwndOwner = CreateWindow(L"static", nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, m_hinstance, nullptr);
    if (!m_hwndOwner)
    {
        Logger::error("Failed to create DimOverlay owner window. GetLastError={}", GetLastError());
        return false;
    }

    const DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOREDIRECTIONBITMAP |
                          WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
    m_hwnd = CreateWindowExW(exStyle,
                             OverlayWindowClassName,
                             L"PowerToys Always On Top Dim Overlay",
                             WS_POPUP,
                             CW_USEDEFAULT,
                             0,
                             CW_USEDEFAULT,
                             0,
                             m_hwndOwner,
                             nullptr,
                             m_hinstance,
                             this);
    if (!m_hwnd)
    {
        Logger::error("Failed to create DimOverlay window. GetLastError={}", GetLastError());
        return false;
    }

    BOOL excludeFromPeek = TRUE;
    DwmSetWindowAttribute(m_hwnd, DWMWA_EXCLUDED_FROM_PEEK, &excludeFromPeek, sizeof(excludeFromPeek));

    UpdateWindowBounds();

    if (!EnsureDispatcherQueue())
    {
        return false;
    }

    if (!EnsureCompositor())
    {
        return false;
    }

    return true;
}

bool DimOverlay::EnsureDispatcherQueue()
{
    if (m_dispatcherQueueController)
    {
        return true;
    }

    DispatcherQueueOptions options = {
        sizeof(DispatcherQueueOptions),
        DQTYPE_THREAD_CURRENT,
        DQTAT_COM_NONE,
    };

    ABI::IDispatcherQueueController* controller = nullptr;
    const HRESULT hr = CreateDispatcherQueueController(options, &controller);
    if (FAILED(hr))
    {
        Logger::error("Failed to create DispatcherQueueController for DimOverlay. HRESULT={:#x}", hr);
        return false;
    }

    *winrt::put_abi(m_dispatcherQueueController) = controller;
    return true;
}

bool DimOverlay::EnsureCompositor()
{
    try
    {
        m_compositor = winrt::Compositor();

        ABI::IDesktopWindowTarget* target = nullptr;
        winrt::check_hresult(m_compositor.as<ABI::ICompositorDesktopInterop>()->CreateDesktopWindowTarget(m_hwnd, false, &target));
        *winrt::put_abi(m_target) = target;

        m_root = m_compositor.CreateContainerVisual();
        m_root.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_target.Root(m_root);

        m_dim = m_compositor.CreateSpriteVisual();
        m_dim.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_dimBrush = m_compositor.CreateColorBrush(GetDefaultDimColor());
        m_dim.Brush(m_dimBrush);
        m_root.Children().InsertAtTop(m_dim);

        m_root.Opacity(0.0f);

        m_opacityAnimation = m_compositor.CreateScalarKeyFrameAnimation();
        m_opacityAnimation.Target(L"Opacity");
        m_opacityAnimation.InsertExpressionKeyFrame(1.0f, L"this.FinalValue");
        BOOL animationsEnabled = GetAnimationsEnabled();
        m_opacityAnimation.Duration(std::chrono::milliseconds{ animationsEnabled ? FadeDurationMs : 1 });

        auto implicitAnimations = m_compositor.CreateImplicitAnimationCollection();
        implicitAnimations.Insert(L"Opacity", m_opacityAnimation);
        m_root.ImplicitAnimations(implicitAnimations);
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error("Failed to create DimOverlay composition resources: {}", winrt::to_string(e.message()));
        return false;
    }

    return true;
}

void DimOverlay::UpdateWindowBounds()
{
    if (!m_hwnd)
    {
        return;
    }

    RECT newBounds{};
    newBounds.left = GetSystemMetrics(SM_XVIRTUALSCREEN);
    newBounds.top = GetSystemMetrics(SM_YVIRTUALSCREEN);
    newBounds.right = newBounds.left + GetSystemMetrics(SM_CXVIRTUALSCREEN);
    newBounds.bottom = newBounds.top + GetSystemMetrics(SM_CYVIRTUALSCREEN);

    if (EqualRect(&newBounds, &m_virtualBounds))
    {
        return;
    }

    m_virtualBounds = newBounds;
    const int width = m_virtualBounds.right - m_virtualBounds.left;
    const int height = m_virtualBounds.bottom - m_virtualBounds.top;

    SetWindowPos(m_hwnd, nullptr, m_virtualBounds.left, m_virtualBounds.top, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
}

void DimOverlay::UpdateRegion(const std::vector<DimOverlayHole>& holes)
{
    if (!m_hwnd)
    {
        return;
    }

    const int width = m_virtualBounds.right - m_virtualBounds.left;
    const int height = m_virtualBounds.bottom - m_virtualBounds.top;
    if (width <= 0 || height <= 0)
    {
        return;
    }

    HRGN baseRegion = CreateRectRgn(0, 0, width, height);
    if (!baseRegion)
    {
        return;
    }

    RECT bounds{ 0, 0, width, height };
    for (const auto& hole : holes)
    {
        RECT local = hole.rect;
        OffsetRect(&local, -m_virtualBounds.left, -m_virtualBounds.top);
        if (!IntersectRect(&local, &local, &bounds))
        {
            continue;
        }

        if (local.right <= local.left || local.bottom <= local.top)
        {
            continue;
        }

        int radius = (std::max)(0, hole.radius);
        const int maxRadius = (std::min)((local.right - local.left) / 2, (local.bottom - local.top) / 2);
        radius = (std::min)(radius, maxRadius);
        HRGN holeRegion = nullptr;
        if (radius > 0)
        {
            const int diameter = radius * 2;
            holeRegion = CreateRoundRectRgn(local.left, local.top, local.right, local.bottom, diameter, diameter);
        }
        else
        {
            holeRegion = CreateRectRgn(local.left, local.top, local.right, local.bottom);
        }

        if (holeRegion)
        {
            CombineRgn(baseRegion, baseRegion, holeRegion, RGN_DIFF);
            DeleteObject(holeRegion);
        }
    }

    if (SetWindowRgn(m_hwnd, baseRegion, TRUE) == 0)
    {
        DeleteObject(baseRegion);
    }
}

void DimOverlay::SetVisible(bool visible)
{
    if (m_visible == visible)
    {
        return;
    }

    if (!m_root || !m_compositor)
    {
        return;
    }

    m_visible = visible;

    if (visible)
    {
        ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    }

    auto batch = m_compositor.CreateScopedBatch(winrt::CompositionBatchTypes::Animation);
    m_root.Opacity(visible ? 1.0f : 0.0f);
    batch.Completed([hwnd = m_hwnd, visible](auto&&, auto&&) {
        if (!visible)
        {
            ShowWindow(hwnd, SW_HIDE);
        }
    });
    batch.End();
}
