#include "pch.h"
#include "ZoneWindowDrawing.h"

#include <string>

namespace NonLocalizable
{
    const wchar_t SegoeUiFont[] = L"Segoe ui";
}

namespace
{
    void InitRGB(_Out_ RGBQUAD* quad, BYTE alpha, COLORREF color)
    {
        ZeroMemory(quad, sizeof(*quad));
        quad->rgbReserved = alpha;
        quad->rgbRed = GetRValue(color) * alpha / 255;
        quad->rgbGreen = GetGValue(color) * alpha / 255;
        quad->rgbBlue = GetBValue(color) * alpha / 255;
    }

    void FillRectARGB(wil::unique_hdc& hdc, RECT const* prcFill, BYTE alpha, COLORREF color, bool blendAlpha)
    {
        BITMAPINFO bi;
        ZeroMemory(&bi, sizeof(bi));
        bi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        bi.bmiHeader.biWidth = 1;
        bi.bmiHeader.biHeight = 1;
        bi.bmiHeader.biPlanes = 1;
        bi.bmiHeader.biBitCount = 32;
        bi.bmiHeader.biCompression = BI_RGB;

        RECT fillRect;
        CopyRect(&fillRect, prcFill);

        RGBQUAD bitmapBits;
        InitRGB(&bitmapBits, alpha, color);
        StretchDIBits(
            hdc.get(),
            fillRect.left,
            fillRect.top,
            fillRect.right - fillRect.left,
            fillRect.bottom - fillRect.top,
            0,
            0,
            1,
            1,
            &bitmapBits,
            &bi,
            DIB_RGB_COLORS,
            SRCCOPY);
    }

    BYTE OpacitySettingToAlpha(int opacity)
    {
        return static_cast<BYTE>(opacity * 2.55);
    }

    void DrawIndex(wil::unique_hdc& hdc, FancyZonesUtils::Rect rect, size_t index)
    {
        Gdiplus::Graphics g(hdc.get());

        Gdiplus::FontFamily fontFamily(NonLocalizable::SegoeUiFont);
        Gdiplus::Font font(&fontFamily, 80, Gdiplus::FontStyleRegular, Gdiplus::UnitPixel);
        Gdiplus::SolidBrush solidBrush(Gdiplus::Color(255, 0, 0, 0));

        std::wstring text = std::to_wstring(index);

        g.SetTextRenderingHint(Gdiplus::TextRenderingHintAntiAlias);
        Gdiplus::StringFormat stringFormat = new Gdiplus::StringFormat();
        stringFormat.SetAlignment(Gdiplus::StringAlignmentCenter);
        stringFormat.SetLineAlignment(Gdiplus::StringAlignmentCenter);

        Gdiplus::RectF gdiRect(static_cast<Gdiplus::REAL>(rect.left()),
                               static_cast<Gdiplus::REAL>(rect.top()),
                               static_cast<Gdiplus::REAL>(rect.width()),
                               static_cast<Gdiplus::REAL>(rect.height()));

        g.DrawString(text.c_str(), -1, &font, gdiRect, &stringFormat, &solidBrush);
    }

    void DrawZone(wil::unique_hdc& hdc, ZoneWindowDrawing::ColorSetting const& colorSetting, winrt::com_ptr<IZone> zone, const std::vector<winrt::com_ptr<IZone>>& zones, bool flashMode) noexcept
    {
        RECT zoneRect = zone->GetZoneRect();

        Gdiplus::Graphics g(hdc.get());
        Gdiplus::Color fillColor(colorSetting.fillAlpha, GetRValue(colorSetting.fill), GetGValue(colorSetting.fill), GetBValue(colorSetting.fill));
        Gdiplus::Color borderColor(colorSetting.borderAlpha, GetRValue(colorSetting.border), GetGValue(colorSetting.border), GetBValue(colorSetting.border));

        Gdiplus::Rect rectangle(zoneRect.left, zoneRect.top, zoneRect.right - zoneRect.left - 1, zoneRect.bottom - zoneRect.top - 1);

        Gdiplus::Pen pen(borderColor, static_cast<Gdiplus::REAL>(colorSetting.thickness));
        g.FillRectangle(new Gdiplus::SolidBrush(fillColor), rectangle);
        g.DrawRectangle(&pen, rectangle);

        if (!flashMode)
        {
            DrawIndex(hdc, zoneRect, zone->Id());
        }
    }
}

namespace ZoneWindowDrawing
{
    void DrawBackdrop(wil::unique_hdc& hdc, RECT const& clientRect) noexcept
    {
        FillRectARGB(hdc, &clientRect, 0, RGB(0, 0, 0), false);
    }

    void DrawActiveZoneSet(wil::unique_hdc& hdc,
                           COLORREF zoneColor,
                           COLORREF zoneBorderColor,
                           COLORREF highlightColor,
                           int zoneOpacity,
                           const std::vector<winrt::com_ptr<IZone>>& zones,
                           const std::vector<size_t>& highlightZones,
                           bool flashMode,
                           bool drawHints) noexcept
    {
        //                                 { fillAlpha, fill, borderAlpha, border, thickness }
        ColorSetting const colorHints{ OpacitySettingToAlpha(zoneOpacity), RGB(81, 92, 107), 255, RGB(104, 118, 138), -2 };
        ColorSetting colorViewer{ OpacitySettingToAlpha(zoneOpacity), 0, 255, RGB(40, 50, 60), -2 };
        ColorSetting colorHighlight{ OpacitySettingToAlpha(zoneOpacity), 0, 255, 0, -2 };
        ColorSetting const colorFlash{ OpacitySettingToAlpha(zoneOpacity), RGB(81, 92, 107), 200, RGB(104, 118, 138), -2 };

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
                if (flashMode)
                {
                    DrawZone(hdc, colorFlash, zone, zones, flashMode);
                }
                else if (drawHints)
                {
                    DrawZone(hdc, colorHints, zone, zones, flashMode);
                }
                {
                    colorViewer.fill = zoneColor;
                    colorViewer.border = zoneBorderColor;
                    DrawZone(hdc, colorViewer, zone, zones, flashMode);
                }
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
                colorHighlight.fill = highlightColor;
                colorHighlight.border = zoneBorderColor;
                DrawZone(hdc, colorHighlight, zone, zones, flashMode);
            }
        }
    }
}
