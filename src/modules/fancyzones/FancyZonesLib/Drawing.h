#pragma once
#include "util.h"

#include <d2d1.h>
#include <d2d1_1.h>
#include <dwrite.h>
#include <wincodec.h>

class Drawing
{
public:
    static D2D1_COLOR_F ConvertColor(COLORREF color);

    Drawing();
    void Init(HWND window);

    operator bool() const;
    void BeginDraw(const D2D1_COLOR_F& backColor);

    winrt::com_ptr<IDWriteTextFormat> CreateTextFormat(LPCWSTR fontFamilyName, FLOAT fontSize, DWRITE_FONT_WEIGHT fontWeight = DWRITE_FONT_WEIGHT_NORMAL) const;
    winrt::com_ptr<ID2D1SolidColorBrush> CreateBrush(D2D1_COLOR_F color) const;
    winrt::com_ptr<ID2D1Bitmap> CreateIcon(HICON icon) const;

    void FillRectangle(const D2D1_RECT_F& rect, D2D1_COLOR_F color);
    void FillRoundedRectangle(const D2D1_RECT_F& rect, D2D1_COLOR_F color);
    void DrawRectangle(const D2D1_RECT_F& rect, D2D1_COLOR_F color, float strokeWidth = 1.0f);
    void DrawRoundedRectangle(const D2D1_RECT_F& rect, D2D1_COLOR_F color, float strokeWidth = 1.0f);
    void DrawTextW(std::wstring text, IDWriteTextFormat* format, const D2D1_RECT_F& rect, D2D1_COLOR_F color);
    void DrawTextTrim(std::wstring text, IDWriteTextFormat* format, const D2D1_RECT_F& rect, D2D1_COLOR_F color);
    void DrawBitmap(const D2D1_RECT_F& rect, ID2D1Bitmap* bitmap);

    void EndDraw();

protected:
    static ID2D1Factory* GetD2DFactory();
    static IDWriteFactory* GetWriteFactory();
    static IWICImagingFactory2* GetImageFactory();

    HWND m_window = nullptr;
    FancyZonesUtils::Rect m_renderRect{};
    winrt::com_ptr<ID2D1RenderTarget> m_renderTarget = nullptr;
};
