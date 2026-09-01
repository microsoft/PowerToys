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

    bool AreEqual(const D2D1_RECT_F& left, const D2D1_RECT_F& right)
    {
        return left.left == right.left &&
               left.top == right.top &&
               left.right == right.right &&
               left.bottom == right.bottom;
    }

    bool AreEqual(const D2D1_ROUNDED_RECT& left, const D2D1_ROUNDED_RECT& right)
    {
        return AreEqual(left.rect, right.rect) &&
               left.radiusX == right.radiusX &&
               left.radiusY == right.radiusY;
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
    m_borderBrush = nullptr;

    const auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(m_window, renderTargetSize, D2D1_PRESENT_OPTIONS_NONE);

    hr = GetD2DFactory()->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, m_renderTarget.put());

    if (!SUCCEEDED(hr) || !m_renderTarget)
    {
        return false;
    }

    m_renderTarget->SetAntialiasMode(D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);

    hr = m_renderTarget->CreateSolidColorBrush(m_sceneRect.borderColor, m_borderBrush.put());
    if (FAILED(hr))
    {
        m_renderTarget = nullptr;
        return false;
    }

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

void FrameDrawer::SetBorderRect(RECT windowRect, COLORREF rgb, float alpha, int thickness, float radius)
{
    auto newSceneRect = DrawableRect{
        .borderColor = ConvertColor(rgb, alpha),
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
    const bool rectangleUpdated = m_sceneRect.rect.has_value() != newSceneRect.rect.has_value() ||
                                  (m_sceneRect.rect && !AreEqual(m_sceneRect.rect.value(), newSceneRect.rect.value()));
    const bool roundedRectangleUpdated = m_sceneRect.roundedRect.has_value() != newSceneRect.roundedRect.has_value() ||
                                         (m_sceneRect.roundedRect && !AreEqual(m_sceneRect.roundedRect.value(), newSceneRect.roundedRect.value()));
    const bool needsRedraw = colorUpdated || thicknessUpdated || rectangleUpdated || roundedRectangleUpdated;

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
        if (m_borderBrush)
        {
            m_borderBrush->SetColor(m_sceneRect.borderColor);
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

D2D1_COLOR_F FrameDrawer::ConvertColor(COLORREF color, float alpha)
{
    return D2D1::ColorF(GetRValue(color) / 255.f,
                        GetGValue(color) / 255.f,
                        GetBValue(color) / 255.f,
                        alpha);
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

winrt::com_ptr<ID2D1Geometry> FrameDrawer::CreateBorderGeometry(const DrawableRect& drawableRect)
{
    const float halfThickness = drawableRect.thickness / 2.0f;
    winrt::com_ptr<ID2D1Geometry> outerGeometry;
    winrt::com_ptr<ID2D1Geometry> innerGeometry;

    if (drawableRect.roundedRect)
    {
        const auto& borderRect = drawableRect.roundedRect.value();
        const auto outerRect = D2D1::RoundedRect(
            D2D1::RectF(
                borderRect.rect.left - halfThickness,
                borderRect.rect.top - halfThickness,
                borderRect.rect.right + halfThickness,
                borderRect.rect.bottom + halfThickness),
            borderRect.radiusX + halfThickness,
            borderRect.radiusY + halfThickness);
        const auto innerRect = D2D1::RoundedRect(
            D2D1::RectF(
                borderRect.rect.left + halfThickness,
                borderRect.rect.top + halfThickness,
                borderRect.rect.right - halfThickness,
                borderRect.rect.bottom - halfThickness),
            (std::max)(borderRect.radiusX - halfThickness, 0.0f),
            (std::max)(borderRect.radiusY - halfThickness, 0.0f));

        winrt::com_ptr<ID2D1RoundedRectangleGeometry> outerRoundedGeometry;
        if (FAILED(GetD2DFactory()->CreateRoundedRectangleGeometry(outerRect, outerRoundedGeometry.put())))
        {
            return nullptr;
        }

        outerGeometry = outerRoundedGeometry.as<ID2D1Geometry>();

        if (innerRect.rect.left >= innerRect.rect.right || innerRect.rect.top >= innerRect.rect.bottom)
        {
            return outerGeometry;
        }

        winrt::com_ptr<ID2D1RoundedRectangleGeometry> innerRoundedGeometry;
        if (FAILED(GetD2DFactory()->CreateRoundedRectangleGeometry(innerRect, innerRoundedGeometry.put())))
        {
            return nullptr;
        }

        innerGeometry = innerRoundedGeometry.as<ID2D1Geometry>();
    }
    else if (drawableRect.rect)
    {
        const auto& borderRect = drawableRect.rect.value();
        const auto outerRect = D2D1::RectF(
            borderRect.left - halfThickness,
            borderRect.top - halfThickness,
            borderRect.right + halfThickness,
            borderRect.bottom + halfThickness);
        const auto innerRect = D2D1::RectF(
            borderRect.left + halfThickness,
            borderRect.top + halfThickness,
            borderRect.right - halfThickness,
            borderRect.bottom - halfThickness);

        winrt::com_ptr<ID2D1RectangleGeometry> outerRectangleGeometry;
        if (FAILED(GetD2DFactory()->CreateRectangleGeometry(outerRect, outerRectangleGeometry.put())))
        {
            return nullptr;
        }

        outerGeometry = outerRectangleGeometry.as<ID2D1Geometry>();

        if (innerRect.left >= innerRect.right || innerRect.top >= innerRect.bottom)
        {
            return outerGeometry;
        }

        winrt::com_ptr<ID2D1RectangleGeometry> innerRectangleGeometry;
        if (FAILED(GetD2DFactory()->CreateRectangleGeometry(innerRect, innerRectangleGeometry.put())))
        {
            return nullptr;
        }

        innerGeometry = innerRectangleGeometry.as<ID2D1Geometry>();
    }

    if (!outerGeometry || !innerGeometry)
    {
        return nullptr;
    }

    ID2D1Geometry* geometries[] = { outerGeometry.get(), innerGeometry.get() };
    winrt::com_ptr<ID2D1GeometryGroup> borderGeometry;
    if (FAILED(GetD2DFactory()->CreateGeometryGroup(D2D1_FILL_MODE_ALTERNATE, geometries, ARRAYSIZE(geometries), borderGeometry.put())))
    {
        return nullptr;
    }

    return borderGeometry.as<ID2D1Geometry>();
}

void FrameDrawer::Render()
{
    if (!m_renderTarget || !m_borderBrush)
    {
        return;
    }

    m_renderTarget->BeginDraw();

    m_renderTarget->Clear(D2D1::ColorF(0.f, 0.f, 0.f, 0.f));

    if (const auto borderGeometry = CreateBorderGeometry(m_sceneRect))
    {
        m_renderTarget->FillGeometry(borderGeometry.get(), m_borderBrush.get());
    }

    const HRESULT hr = m_renderTarget->EndDraw();
    if (hr == D2DERR_RECREATE_TARGET)
    {
        m_borderBrush = nullptr;
        m_renderTarget = nullptr;
        m_renderTargetSizeHash = {};
    }
    else if (FAILED(hr))
    {
        Logger::error(L"Failed to render the border. HRESULT: 0x{:08X}", static_cast<unsigned long>(hr));
    }
}
