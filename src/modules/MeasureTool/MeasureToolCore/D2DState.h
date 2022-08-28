#pragma once

#include <optional>
#include <vector>

#include <d2d1_3.h>
#include <wil/com.h>
#include <windef.h>

#include "PerGlyphOpacityTextRender.h"

enum Brush : size_t
{
    line,
    foreground,
    background,
    border
};

struct D2DState
{
    wil::com_ptr<ID2D1Factory> d2dFactory;
    wil::com_ptr<IDWriteFactory> writeFactory;
    wil::com_ptr<ID2D1HwndRenderTarget> rt;
    wil::com_ptr<ID2D1BitmapRenderTarget> bitmapRt;
    wil::com_ptr<IDWriteTextFormat> textFormat;
    winrt::com_ptr<PerGlyphOpacityTextRender> textRenderer; 
    std::vector<wil::com_ptr<ID2D1SolidColorBrush>> solidBrushes;
    wil::com_ptr<ID2D1Effect> shadowEffect;
    wil::com_ptr<ID2D1Effect> affineTransformEffect;

    float dpiScale = 1.f;

    D2DState(const HWND window, std::vector<D2D1::ColorF> solidBrushesColors);
    void DrawTextBox(const wchar_t* text,
                     const uint32_t textLen,
                     const std::optional<size_t> halfOpaqueSymbolPos,
                     const float centerX,
                     const float centerY,
                     const bool screenQuadrantAware,
                     const HWND window) const;
};
