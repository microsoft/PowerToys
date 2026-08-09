#pragma once

#include <optional>
#include <vector>

#include <windef.h>

#include "DxgiAPI.h"
#include "PerGlyphOpacityTextRender.h"

enum Brush : size_t
{
    line,
    foreground,
    background,
    border
};

enum class TextBoxPlacement
{
    Centered,
    CursorQuadrant,
    AboveAnchor,
    OutsideRectangle,
};

struct D2DState
{
    const DxgiAPI* dxgiAPI = nullptr;

    DxgiWindowState dxgiWindowState;
    winrt::com_ptr<ID2D1BitmapRenderTarget> bitmapRt;
    winrt::com_ptr<IDWriteTextFormat> textFormat;
    winrt::com_ptr<PerGlyphOpacityTextRender> textRenderer;
    std::vector<winrt::com_ptr<ID2D1SolidColorBrush>> solidBrushes;
    winrt::com_ptr<ID2D1Effect> shadowEffect;
    winrt::com_ptr<ID2D1Effect> affineTransformEffect;

    float dpiScale = 1.f;

    D2DState(const DxgiAPI*,
             HWND window,
             std::vector<D2D1::ColorF> solidBrushesColors);
    void DrawTextBox(const wchar_t* text,
                     const size_t textLen,
                     const size_t halfOpaqueSymbolPos[2],
                     const D2D_POINT_2F anchor,
                     TextBoxPlacement placement,
                     HWND window,
                     std::optional<D2D1_RECT_F> referenceRect = std::nullopt) const;
    void ToggleAliasedLinesMode(const bool enabled) const;
};
