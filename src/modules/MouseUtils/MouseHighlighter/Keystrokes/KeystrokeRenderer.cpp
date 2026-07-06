// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "KeystrokeRenderer.h"

#include <windows.ui.composition.interop.h>

#include <winrt/Windows.Graphics.DirectX.h>

#include <algorithm>
#include <cmath>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dwrite.lib")
#pragma comment(lib, "dxgi.lib")

// This file performs a lot of low-level D2D/DWrite/Composition interop; several
// C++ Core Guidelines analysis checks are noisy here and are suppressed to match
// the pragmatic style used elsewhere in the native rendering code.
#pragma warning(disable : 26451 26429 26446 26447 26461 26472 26481 26490 26493 26496 26497 26812)

namespace ABI
{
    using namespace ABI::Windows::UI::Composition;
}

using namespace winrt::Windows::UI;
using namespace winrt::Windows::UI::Composition;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Numerics;

namespace WGDX = winrt::Windows::Graphics::DirectX;

namespace InputHighlighter
{
    namespace
    {
        constexpr float kMarginDip = 24.0f;
        constexpr float kGapDip = 10.0f;
        constexpr wchar_t kFontFamily[] = L"Segoe UI";
    }

    KeystrokeRenderer::~KeystrokeRenderer()
    {
        // Drop references without touching the (possibly torn-down) parent tree.
        m_pills.clear();
        m_container = nullptr;
        m_graphicsDevice = nullptr;
    }

    bool KeystrokeRenderer::CreateDevices()
    {
        UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
        HRESULT hr = D3D11CreateDevice(
            nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags, nullptr, 0, D3D11_SDK_VERSION, m_d3dDevice.put(), nullptr, nullptr);
        if (hr == DXGI_ERROR_UNSUPPORTED)
        {
            hr = D3D11CreateDevice(
                nullptr, D3D_DRIVER_TYPE_WARP, nullptr, flags, nullptr, 0, D3D11_SDK_VERSION, m_d3dDevice.put(), nullptr, nullptr);
        }
        if (FAILED(hr))
        {
            return false;
        }

        auto dxgiDevice = m_d3dDevice.as<IDXGIDevice>();

        D2D1_FACTORY_OPTIONS d2dOptions{};
        hr = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof(ID2D1Factory1), &d2dOptions, m_d2dFactory.put_void());
        if (FAILED(hr))
        {
            return false;
        }

        hr = m_d2dFactory->CreateDevice(dxgiDevice.get(), m_d2dDevice.put());
        if (FAILED(hr))
        {
            return false;
        }

        hr = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), reinterpret_cast<::IUnknown**>(m_dwriteFactory.put()));
        if (FAILED(hr))
        {
            return false;
        }

        // Bind the D2D device to the compositor to create Composition-hosted surfaces.
        auto interop = m_compositor.as<ABI::ICompositorInterop>();
        ABI::ICompositionGraphicsDevice* rawDevice = nullptr;
        hr = interop->CreateGraphicsDevice(m_d2dDevice.get(), &rawDevice);
        if (FAILED(hr))
        {
            return false;
        }
        winrt::attach_abi(m_graphicsDevice, rawDevice);
        return true;
    }

    bool KeystrokeRenderer::Initialize(const Compositor& compositor, const ContainerVisual& parentRoot, HWND hwnd)
    {
        if (m_initialized)
        {
            return true;
        }

        m_compositor = compositor;
        m_hwnd = hwnd;

        try
        {
            if (!CreateDevices())
            {
                return false;
            }

            m_container = m_compositor.CreateContainerVisual();
            m_container.RelativeSizeAdjustment({ 1.0f, 1.0f });
            parentRoot.Children().InsertAtTop(m_container);
        }
        catch (...)
        {
            return false;
        }

        m_initialized = true;
        return true;
    }

    void KeystrokeRenderer::Uninitialize()
    {
        if (!m_initialized)
        {
            return;
        }

        Clear();

        if (m_container)
        {
            try
            {
                auto parent = m_container.Parent();
                if (parent)
                {
                    parent.Children().Remove(m_container);
                }
            }
            catch (...)
            {
            }
        }

        m_container = nullptr;
        m_graphicsDevice = nullptr;
        m_d2dDevice = nullptr;
        m_d2dFactory = nullptr;
        m_dwriteFactory = nullptr;
        m_d3dDevice = nullptr;
        m_initialized = false;
    }

    void KeystrokeRenderer::ApplySettings(const KeystrokeRendererSettings& settings)
    {
        m_settings = settings;
        // Mode / style changes reset the current stack; new pills pick up the new look.
        Clear();
    }

    void KeystrokeRenderer::SetAnchorRect(const D2D1_RECT_F& clientRect)
    {
        m_anchorRect = clientRect;
        Relayout();
    }

    float KeystrokeRenderer::DpiScale() const
    {
        const UINT dpi = m_hwnd ? GetDpiForWindow(m_hwnd) : 96;
        return static_cast<float>(dpi == 0 ? 96 : dpi) / 96.0f;
    }

    size_t KeystrokeRenderer::MaxPills() const
    {
        switch (m_settings.displayMode)
        {
        case DisplayMode::Last5:
            return 5;
        case DisplayMode::Stream:
            return 8;
        case DisplayMode::SingleCharactersOnly:
        case DisplayMode::ShortcutsOnly:
        default:
            return 5;
        }
    }

    void KeystrokeRenderer::DrawPill(Pill& pill, const std::wstring& text)
    {
        if (!m_initialized)
        {
            return;
        }

        const float dpiScale = DpiScale();
        const float fontPx = (std::max)(8.0f, m_settings.textSize) * dpiScale;

        // Measure the string with DirectWrite.
        winrt::com_ptr<IDWriteTextFormat> format;
        if (FAILED(m_dwriteFactory->CreateTextFormat(
                kFontFamily, nullptr, DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, fontPx, L"", format.put())))
        {
            return;
        }
        format->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
        format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);

        winrt::com_ptr<IDWriteTextLayout> layout;
        if (FAILED(m_dwriteFactory->CreateTextLayout(
                text.c_str(), static_cast<UINT32>(text.length()), format.get(), 4096.0f, 4096.0f, layout.put())))
        {
            return;
        }

        DWRITE_TEXT_METRICS metrics{};
        layout->GetMetrics(&metrics);

        const float padX = fontPx * 0.55f;
        const float padY = fontPx * 0.30f;
        float widthPx = std::ceil(metrics.width + 2.0f * padX);
        float heightPx = std::ceil(metrics.height + 2.0f * padY);
        widthPx = (std::max)(widthPx, heightPx); // keep short pills from looking cramped

        layout->SetMaxWidth(widthPx);
        layout->SetMaxHeight(heightPx);

        // (Re)create the drawing surface at the measured pixel size.
        auto surface = m_graphicsDevice.CreateDrawingSurface(
            Size{ widthPx, heightPx }, WGDX::DirectXPixelFormat::B8G8R8A8UIntNormalized, WGDX::DirectXAlphaMode::Premultiplied);

        auto surfaceInterop = surface.as<ABI::ICompositionDrawingSurfaceInterop>();
        winrt::com_ptr<ID2D1DeviceContext> dc;
        POINT offset{};
        if (FAILED(surfaceInterop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), dc.put_void(), &offset)))
        {
            return;
        }

        dc->SetDpi(96.0f, 96.0f);
        dc->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x), static_cast<float>(offset.y)));
        dc->Clear(D2D1::ColorF(0.0f, 0.0f, 0.0f, 0.0f));

        const auto& bg = m_settings.backgroundColor;
        const auto& fg = m_settings.textColor;

        winrt::com_ptr<ID2D1SolidColorBrush> bgBrush;
        dc->CreateSolidColorBrush(D2D1::ColorF(bg.R / 255.0f, bg.G / 255.0f, bg.B / 255.0f, bg.A / 255.0f), bgBrush.put());
        winrt::com_ptr<ID2D1SolidColorBrush> fgBrush;
        dc->CreateSolidColorBrush(D2D1::ColorF(fg.R / 255.0f, fg.G / 255.0f, fg.B / 255.0f, fg.A / 255.0f), fgBrush.put());

        const float radius = heightPx * 0.28f;
        const D2D1_ROUNDED_RECT rr = D2D1::RoundedRect(D2D1::RectF(0.5f, 0.5f, widthPx - 0.5f, heightPx - 0.5f), radius, radius);
        if (bgBrush)
        {
            dc->FillRoundedRectangle(rr, bgBrush.get());
        }
        if (fgBrush)
        {
            dc->DrawTextLayout(D2D1::Point2F(0.0f, 0.0f), layout.get(), fgBrush.get(), D2D1_DRAW_TEXT_OPTIONS_NONE);
        }
        if (m_settings.strokeThickness > 0 && m_settings.strokeColor.A > 0)
        {
            const auto& sc = m_settings.strokeColor;
            winrt::com_ptr<ID2D1SolidColorBrush> strokeBrush;
            dc->CreateSolidColorBrush(D2D1::ColorF(sc.R / 255.0f, sc.G / 255.0f, sc.B / 255.0f, sc.A / 255.0f), strokeBrush.put());
            if (strokeBrush)
            {
                const float sw = m_settings.strokeThickness * DpiScale();
                const float inset = 0.5f + sw / 2.0f;
                const D2D1_ROUNDED_RECT sr = D2D1::RoundedRect(D2D1::RectF(inset, inset, widthPx - inset, heightPx - inset), radius, radius);
                dc->DrawRoundedRectangle(sr, strokeBrush.get(), sw);
            }
        }

        surfaceInterop->EndDraw();

        auto brush = m_compositor.CreateSurfaceBrush(surface);
        brush.Stretch(CompositionStretch::Fill);

        if (!pill.visual)
        {
            pill.visual = m_compositor.CreateSpriteVisual();
            m_container.Children().InsertAtTop(pill.visual);
        }
        pill.visual.Brush(brush);
        pill.visual.Size({ widthPx, heightPx });
        pill.brush = brush;
        pill.surface = surface;
        pill.text = text;
        pill.width = widthPx;
        pill.height = heightPx;
    }

    void KeystrokeRenderer::AnimateEntrance(const Pill& pill, float /*targetOpacity*/)
    {
        if (!pill.visual)
        {
            return;
        }
        pill.visual.CenterPoint({ pill.width / 2.0f, pill.height / 2.0f, 0.0f });
        auto anim = m_compositor.CreateVector3KeyFrameAnimation();
        anim.InsertKeyFrame(0.0f, { 0.7f, 0.7f, 1.0f });
        anim.InsertKeyFrame(1.0f, { 1.0f, 1.0f, 1.0f });
        anim.Duration(std::chrono::milliseconds(120));
        pill.visual.StartAnimation(L"Scale", anim);
    }

    void KeystrokeRenderer::EnforceCap()
    {
        const size_t cap = MaxPills();
        while (m_pills.size() > cap)
        {
            auto& front = m_pills.front();
            if (front.visual && m_container)
            {
                m_container.Children().Remove(front.visual);
            }
            m_pills.pop_front();
        }
    }

    void KeystrokeRenderer::Relayout()
    {
        if (!m_initialized || m_pills.empty())
        {
            return;
        }

        const float dpiScale = DpiScale();
        const float margin = kMarginDip * dpiScale;
        const float gap = kGapDip * dpiScale;

        float totalW = 0.0f;
        float maxH = 0.0f;
        for (const auto& p : m_pills)
        {
            totalW += p.width;
            maxH = (std::max)(maxH, p.height);
        }
        totalW += gap * static_cast<float>(m_pills.size() - 1);

        const float left = m_anchorRect.left + margin;
        const float right = m_anchorRect.right - margin;
        const float centerX = (m_anchorRect.left + m_anchorRect.right) / 2.0f;

        float startX = left;
        switch (m_settings.position)
        {
        case KeystrokePosition::TopLeft:
        case KeystrokePosition::BottomLeft:
            startX = left;
            break;
        case KeystrokePosition::TopCenter:
        case KeystrokePosition::BottomCenter:
            startX = centerX - totalW / 2.0f;
            break;
        case KeystrokePosition::TopRight:
        case KeystrokePosition::BottomRight:
            startX = right - totalW;
            break;
        }

        float baseY = m_anchorRect.top + margin;
        switch (m_settings.position)
        {
        case KeystrokePosition::BottomLeft:
        case KeystrokePosition::BottomCenter:
        case KeystrokePosition::BottomRight:
            baseY = m_anchorRect.bottom - margin - maxH;
            break;
        default:
            break;
        }

        const size_t n = m_pills.size();
        float x = startX;
        for (size_t i = 0; i < n; ++i)
        {
            auto& p = m_pills[i];
            const float y = baseY + (maxH - p.height) / 2.0f;
            p.visual.Offset({ x, y, 0.0f });

            // Older pills (towards the front) fade out slightly.
            const size_t ageFromNewest = (n - 1) - i;
            const float opacity = (std::max)(0.4f, 1.0f - 0.12f * static_cast<float>(ageFromNewest));
            p.visual.Opacity(opacity);

            x += p.width + gap;
        }
    }

    void KeystrokeRenderer::OnResult(const KeystrokeResult& result)
    {
        if (!m_initialized || result.action == KeystrokeAction::None)
        {
            return;
        }

        const ULONGLONG deadline = (m_settings.timeoutMs > 0) ? (GetTickCount64() + static_cast<ULONGLONG>(m_settings.timeoutMs)) : 0;

        switch (result.action)
        {
        case KeystrokeAction::Add:
        {
            Pill pill;
            DrawPill(pill, result.text);
            pill.expireAt = deadline;
            m_pills.push_back(std::move(pill));
            EnforceCap();
            Relayout();
            AnimateEntrance(m_pills.back(), 1.0f);
            break;
        }
        case KeystrokeAction::ReplaceLast:
        {
            if (m_pills.empty())
            {
                Pill pill;
                DrawPill(pill, result.text);
                pill.expireAt = deadline;
                m_pills.push_back(std::move(pill));
                EnforceCap();
                Relayout();
                AnimateEntrance(m_pills.back(), 1.0f);
            }
            else
            {
                auto& back = m_pills.back();
                DrawPill(back, result.text);
                back.expireAt = deadline;
                Relayout();
            }
            break;
        }
        case KeystrokeAction::RemoveLast:
        {
            if (!m_pills.empty())
            {
                auto& back = m_pills.back();
                if (back.visual && m_container)
                {
                    m_container.Children().Remove(back.visual);
                }
                m_pills.pop_back();
                Relayout();
            }
            break;
        }
        case KeystrokeAction::None:
        default:
            break;
        }
    }

    bool KeystrokeRenderer::Tick()
    {
        if (m_pills.empty())
        {
            return false;
        }

        const ULONGLONG now = GetTickCount64();
        bool changed = false;

        // Oldest pills (front) expire first.
        while (!m_pills.empty())
        {
            auto& front = m_pills.front();
            if (front.expireAt == 0 || now < front.expireAt)
            {
                break;
            }
            if (front.visual && m_container)
            {
                m_container.Children().Remove(front.visual);
            }
            m_pills.pop_front();
            changed = true;
        }

        if (changed)
        {
            Relayout();
        }
        return changed;
    }

    void KeystrokeRenderer::Clear()
    {
        if (m_container)
        {
            for (auto& p : m_pills)
            {
                if (p.visual)
                {
                    m_container.Children().Remove(p.visual);
                }
            }
        }
        m_pills.clear();
    }
}
