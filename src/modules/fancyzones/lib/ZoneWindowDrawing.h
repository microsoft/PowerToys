#pragma once

#include <map>
#include <vector>
#include <wil\resource.h>
#include <winrt/base.h>
#include <d2d1.h>
#include <dwrite.h>

#include "util.h"
#include "Zone.h"
#include "ZoneSet.h"

namespace ZoneWindowDrawingNS
{
    struct ColorSetting
    {
        BYTE fillAlpha{};
        COLORREF fill{};
        BYTE borderAlpha{};
        COLORREF border{};
        int thickness{};
    };

    void DrawBackdrop(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void DrawActiveZoneSet(wil::unique_hdc& hdc,
                           COLORREF zoneColor,
                           COLORREF zoneBorderColor,
                           COLORREF highlightColor,
                           int zoneOpacity,
                           const IZoneSet::ZonesMap& zones,
                           const std::vector<size_t>& highlightZones,
                           bool flashMode) noexcept;
}

class ZoneWindowDrawing
{
    struct DrawableRect
    {
        D2D1_RECT_F rect;
        D2D1_COLOR_F borderColor;
        D2D1_COLOR_F fillColor;
        size_t id;
    };

    HWND m_window;
    RECT m_clientRect;
    // winrt::com_ptr<IZoneWindowHost> m_host;
    ID2D1HwndRenderTarget* m_renderTarget;

    std::mutex m_sceneMutex;
    std::vector<DrawableRect> m_sceneRects;

    void DrawBackdrop()
    {
        m_renderTarget->Clear(D2D1::ColorF(0.f, 0.f, 0.f, 0.f));
    }

    static ID2D1Factory* GetD2DFactory()
    {
        static ID2D1Factory* pD2DFactory = nullptr;
        if (!pD2DFactory)
        {
            D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &pD2DFactory);
        }
        return pD2DFactory;

        // TODO: Destroy factory
    }

    static IDWriteFactory* GetWriteFactory()
    {
        static IUnknown* pDWriteFactory = nullptr;
        if (!pDWriteFactory)
        {
            DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &pDWriteFactory);
        }
        return reinterpret_cast<IDWriteFactory*>(pDWriteFactory);

        // TODO: Destroy factory
    }

    static D2D1_COLOR_F ConvertColor(COLORREF color)
    {
        return D2D1::ColorF(GetRValue(color) / 255.f,
                            GetGValue(color) / 255.f,
                            GetBValue(color) / 255.f,
                            1.f);
    }

    static D2D1_RECT_F ConvertRect(RECT rect)
    {
        return D2D1::RectF(rect.left, rect.top, rect.right, rect.bottom);
    }

public:
    ZoneWindowDrawing(HWND window)
    {
        m_window = window;
        m_renderTarget = nullptr;

        // Obtain the size of the drawing area.
        if (!GetClientRect(window, &m_clientRect))
        {
            return;
        }

        // Create a Direct2D render target
        GetD2DFactory()->CreateHwndRenderTarget(
            D2D1::RenderTargetProperties(
                D2D1_RENDER_TARGET_TYPE_DEFAULT,
                D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED)),
            D2D1::HwndRenderTargetProperties(
                window,
                D2D1::SizeU(
                    m_clientRect.right - m_clientRect.left,
                    m_clientRect.bottom - m_clientRect.top)),
            &m_renderTarget);
    }

    void Render()
    {
        std::unique_lock lock(m_sceneMutex);

        if (!m_renderTarget)
        {
            return;
        }

        m_renderTarget->BeginDraw();
        DrawBackdrop();



        for (const auto& drawableRect : m_sceneRects)
        {
            ID2D1SolidColorBrush* borderBrush = nullptr;
            ID2D1SolidColorBrush* fillBrush = nullptr;
            ID2D1SolidColorBrush* textBrush = nullptr;
            m_renderTarget->CreateSolidColorBrush(drawableRect.borderColor, &borderBrush);
            m_renderTarget->CreateSolidColorBrush(drawableRect.fillColor, &fillBrush);
            m_renderTarget->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Black), &textBrush);

            if (fillBrush)
            {
                m_renderTarget->FillRectangle(drawableRect.rect, fillBrush);
                fillBrush->Release();
            }

            if (borderBrush)
            {
                m_renderTarget->DrawRectangle(drawableRect.rect, borderBrush);
                borderBrush->Release();
            }

            std::wstring idStr = std::to_wstring(drawableRect.id);

            // TODO: mark string as non-localizable
            IDWriteTextFormat* textFormat = nullptr;
            auto writeFactory = GetWriteFactory();

            if (writeFactory)
            {
                writeFactory->CreateTextFormat(L"Segoe ui", nullptr, DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 80.f, L"en-US", &textFormat);
            }
            
            if (textFormat && textBrush)
            {
                textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
                textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
                m_renderTarget->DrawTextW(idStr.c_str(), idStr.size(), textFormat, drawableRect.rect, textBrush);
            }

            if (textFormat)
            {
                textFormat->Release();
            }

            if (textBrush)
            {
                textBrush->Release();
            }
        }

        m_renderTarget->EndDraw();
    }

    void StartAnimation(unsigned millis)
    {

    }

    void DrawActiveZoneSet(const std::vector<winrt::com_ptr<IZone>>& zones,
                           const std::vector<size_t>& highlightZones,
                           winrt::com_ptr<IZoneWindowHost> host)
    {
        std::unique_lock lock(m_sceneMutex);

        m_sceneRects = {};

        auto borderColor = ConvertColor(host->GetZoneBorderColor());
        auto inactiveColor = ConvertColor(host->GetZoneColor());
        auto highlightColor = ConvertColor(host->GetZoneHighlightColor());

        inactiveColor.a = host->GetZoneHighlightOpacity() / 255.f;
        highlightColor.a = host->GetZoneHighlightOpacity() / 255.f;

        std::vector<bool> isHighlighted(zones.size(), false);
        for (size_t x : highlightZones)
        {
            isHighlighted[x] = true;
        }

        // First draw the inactive zones
        for (auto iter = zones.begin(); iter != zones.end(); iter++)
        {
            int zoneId = static_cast<int>(iter - zones.begin());
            winrt::com_ptr<IZone> zone = iter->try_as<IZone>();
            if (!zone)
            {
                continue;
            }

            if (!isHighlighted[zoneId])
            {
                DrawableRect drawableRect{
                    .rect = ConvertRect(zone->GetZoneRect()),
                    .borderColor = borderColor,
                    .fillColor = inactiveColor,
                    .id = zone->Id()
                };

                m_sceneRects.push_back(drawableRect);
            }
        }

        // Draw the active zones on top of the inactive zones
        for (auto iter = zones.begin(); iter != zones.end(); iter++)
        {
            int zoneId = static_cast<int>(iter - zones.begin());
            winrt::com_ptr<IZone> zone = iter->try_as<IZone>();
            if (!zone)
            {
                continue;
            }

            if (isHighlighted[zoneId])
            {
                DrawableRect drawableRect{
                    .rect = ConvertRect(zone->GetZoneRect()),
                    .borderColor = borderColor,
                    .fillColor = highlightColor,
                    .id = zone->Id()
                };

                m_sceneRects.push_back(drawableRect);
            }
        }
    }
};
