#include "pch.h"
#include "FrameDrawer.h"

#include <dwmapi.h>

#include <ScalingUtils.h>

namespace
{
    size_t D2DRectUHash(D2D1_SIZE_U rect)
    {
        using pod_repr_t = uint64_t;
        static_assert(sizeof(D2D1_SIZE_U) == sizeof(pod_repr_t));
        std::hash<pod_repr_t> hasher{};
        return hasher(*reinterpret_cast<const pod_repr_t*>(&rect));
    }
}

std::unique_ptr<FrameDrawer> FrameDrawer::Create(HWND window)
{
    auto self = std::make_unique<FrameDrawer>(window);
    if (self->Init())
    {
        return self;
    }

    return nullptr;
}

FrameDrawer::FrameDrawer(HWND window) :
    m_window(window)
{
}

bool FrameDrawer::CreateRenderTargets(const RECT& clientRect)
{
    HRESULT hr;

    constexpr float DPI = 96.f; // Always using the default in DPI-aware mode
    const auto renderTargetProperties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED),
        DPI,
        DPI);

    const auto renderTargetSize = D2D1::SizeU(clientRect.right - clientRect.left, clientRect.bottom - clientRect.top);
    const auto rectHash = D2DRectUHash(renderTargetSize);
    if (m_renderTarget && rectHash == m_renderTargetSizeHash)
    {
        // Already at the desired size -> do nothing
        return true;
    }

    m_renderTarget = nullptr;

    const auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(m_window, renderTargetSize, D2D1_PRESENT_OPTIONS_NONE);

    hr = GetD2DFactory()->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, m_renderTarget.put());

    if (!SUCCEEDED(hr) || !m_renderTarget)
    {
        return false;
    }

    m_renderTarget->SetAntialiasMode(D2D1_ANTIALIAS_MODE_ALIASED);
    m_renderTargetSizeHash = rectHash;

    return true;
}

bool FrameDrawer::Init()
{
    RECT clientRect;
    if (!SUCCEEDED(DwmGetWindowAttribute(m_window, DWMWA_EXTENDED_FRAME_BOUNDS, &clientRect, sizeof(clientRect))))
    {
        return false;
    }

    return CreateRenderTargets(clientRect);
}

void FrameDrawer::Hide()
{
    ShowWindow(m_window, SW_HIDE);
}

void FrameDrawer::Show()
{
    ShowWindow(m_window, SW_SHOWNA);
    Render();
}

void FrameDrawer::SetBorderRect(RECT windowRect, COLORREF color, int thickness, float radius)
{
    auto newSceneRect = DrawableRect{
        .borderColor = ConvertColor(color),
        .thickness = thickness,
    };

    if (radius != 0)
    {
        newSceneRect.roundedRect = ConvertRect(windowRect, thickness, radius);
    }
    else
    {
        newSceneRect.rect = ConvertRect(windowRect, thickness);
    }
    
    const bool colorUpdated = std::memcmp(&m_sceneRect.borderColor, &newSceneRect.borderColor, sizeof(newSceneRect.borderColor));
    const bool thicknessUpdated = m_sceneRect.thickness != newSceneRect.thickness;
    const bool cornersUpdated = m_sceneRect.rect.has_value() != newSceneRect.rect.has_value() || m_sceneRect.roundedRect.has_value() != newSceneRect.roundedRect.has_value();
    const bool needsRedraw = colorUpdated || thicknessUpdated || cornersUpdated;

    RECT clientRect;
    if (!SUCCEEDED(DwmGetWindowAttribute(m_window, DWMWA_EXTENDED_FRAME_BOUNDS, &clientRect, sizeof(clientRect))))
    {
        return;
    }

    m_sceneRect = std::move(newSceneRect);

    const auto renderTargetSize = D2D1::SizeU(clientRect.right - clientRect.left, clientRect.bottom - clientRect.top);

    const auto rectHash = D2DRectUHash(renderTargetSize);

    const bool atTheDesiredSize = (rectHash == m_renderTargetSizeHash) && m_renderTarget;
    if (!atTheDesiredSize)
    {
        const bool resizeOk = m_renderTarget && SUCCEEDED(m_renderTarget->Resize(renderTargetSize));
        if (!resizeOk)
        {
            if (!CreateRenderTargets(clientRect))
            {
                Logger::error(L"Failed to create render targets");
            }
        }
        else
        {
            m_renderTargetSizeHash = rectHash;
        }
    }

    if (colorUpdated)
    {
        m_borderBrush = nullptr;
        if (m_renderTarget)
        {
            m_renderTarget->CreateSolidColorBrush(m_sceneRect.borderColor, m_borderBrush.put());
        }
    }

    if (!atTheDesiredSize || needsRedraw)
    {
        Render();
    }
}

ID2D1Factory* FrameDrawer::GetD2DFactory()
{
    static auto pD2DFactory = [] {
        ID2D1Factory* res = nullptr;
        D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &res);
        return res;
    }();
    return pD2DFactory;
}

IDWriteFactory* FrameDrawer::GetWriteFactory()
{
    static auto pDWriteFactory = [] {
        IUnknown* res = nullptr;
        DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &res);
        return reinterpret_cast<IDWriteFactory*>(res);
    }();
    return pDWriteFactory;
}

D2D1_COLOR_F FrameDrawer::ConvertColor(COLORREF color)
{
    return D2D1::ColorF(GetRValue(color) / 255.f,
                        GetGValue(color) / 255.f,
                        GetBValue(color) / 255.f,
                        1.f);
}

D2D1_ROUNDED_RECT FrameDrawer::ConvertRect(RECT rect, int thickness, float radius)
{
    float halfThickness = thickness / 2.0f;

    // 1 is needed to eliminate the gap between border and window
    auto d2d1Rect = D2D1::RectF(static_cast<float>(rect.left) + halfThickness + 1, 
        static_cast<float>(rect.top) + halfThickness + 1, 
        static_cast<float>(rect.right) - halfThickness - 1, 
        static_cast<float>(rect.bottom) - halfThickness - 1);
    return D2D1::RoundedRect(d2d1Rect, radius, radius);
}

D2D1_RECT_F FrameDrawer::ConvertRect(RECT rect, int thickness)
{
    float halfThickness = thickness / 2.0f;

    // 1 is needed to eliminate the gap between border and window
    return D2D1::RectF(static_cast<float>(rect.left) + halfThickness + 1,
        static_cast<float>(rect.top) + halfThickness + 1,
        static_cast<float>(rect.right) - halfThickness - 1,
        static_cast<float>(rect.bottom) - halfThickness - 1);
}

void FrameDrawer::Render()
{
    if (!m_renderTarget || !m_borderBrush)
    {
        return;
    }

    m_renderTarget->BeginDraw();

    m_renderTarget->Clear(D2D1::ColorF(0.f, 0.f, 0.f, 0.f));

    // The border stroke is centered on the line.

    if (m_sceneRect.roundedRect)
    {
        m_renderTarget->DrawRoundedRectangle(m_sceneRect.roundedRect.value(), m_borderBrush.get(), static_cast<float>(m_sceneRect.thickness));
    }
    else if (m_sceneRect.rect)
    {
        m_renderTarget->DrawRectangle(m_sceneRect.rect.value(), m_borderBrush.get(), static_cast<float>(m_sceneRect.thickness));
    }
    
    m_renderTarget->EndDraw();
}