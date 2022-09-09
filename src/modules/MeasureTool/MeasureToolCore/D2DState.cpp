#include "pch.h"

#include "constants.h"
#include "D2DState.h"
#include "DxgiAPI.h"

#include <common/Display/dpi_aware.h>
#include <ToolState.h>

namespace
{
    void DetermineScreenQuadrant(const HWND window, long x, long y, bool& inLeftHalf, bool& inTopHalf)
    {
        RECT windowRect{};
        GetWindowRect(window, &windowRect);
        const long w = windowRect.right - windowRect.left;
        const long h = windowRect.bottom - windowRect.top;
        inLeftHalf = x < w / 2;
        inTopHalf = y < h / 2;
    }
}

D2DState::D2DState(const DxgiAPI* dxgi,
                   HWND window,
                   std::vector<D2D1::ColorF> solidBrushesColors)
{
    dxgiAPI = dxgi;

    unsigned dpi = DPIAware::DEFAULT_DPI;
    DPIAware::GetScreenDPIForWindow(window, dpi);
    dpiScale = dpi / static_cast<float>(DPIAware::DEFAULT_DPI);

    dxgiWindowState = dxgiAPI->CreateD2D1RenderTarget(window);

    winrt::check_hresult(dxgiWindowState.rt->CreateCompatibleRenderTarget(bitmapRt.put()));

    winrt::check_hresult(dxgiAPI->writeFactory->CreateTextFormat(L"Segoe UI Variable Text",
                                                                 nullptr,
                                                                 DWRITE_FONT_WEIGHT_NORMAL,
                                                                 DWRITE_FONT_STYLE_NORMAL,
                                                                 DWRITE_FONT_STRETCH_NORMAL,
                                                                 consts::FONT_SIZE * dpiScale,
                                                                 L"en-US",
                                                                 textFormat.put()));
    winrt::check_hresult(textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));
    winrt::check_hresult(textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER));
    winrt::check_hresult(textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP));

    solidBrushes.resize(solidBrushesColors.size());
    for (size_t i = 0; i < solidBrushes.size(); ++i)
    {
        winrt::check_hresult(dxgiWindowState.rt->CreateSolidColorBrush(solidBrushesColors[i], solidBrushes[i].put()));
    }

    const auto deviceContext = dxgiWindowState.rt.as<ID2D1DeviceContext>();
    winrt::check_hresult(deviceContext->CreateEffect(CLSID_D2D1Shadow, shadowEffect.put()));
    winrt::check_hresult(shadowEffect->SetValue(D2D1_SHADOW_PROP_BLUR_STANDARD_DEVIATION, consts::SHADOW_RADIUS));
    winrt::check_hresult(shadowEffect->SetValue(D2D1_SHADOW_PROP_COLOR, D2D1::ColorF(0.f, 0.f, 0.f, consts::SHADOW_OPACITY)));

    winrt::check_hresult(deviceContext->CreateEffect(CLSID_D2D12DAffineTransform, affineTransformEffect.put()));
    affineTransformEffect->SetInputEffect(0, shadowEffect.get());

    textRenderer = winrt::make_self<PerGlyphOpacityTextRender>(dxgi->d2dFactory2, dxgiWindowState.rt, solidBrushes[Brush::foreground]);
}

void D2DState::DrawTextBox(const wchar_t* text,
                           const size_t textLen,
                           const std::optional<size_t> halfOpaqueSymbolPos,
                           const D2D_POINT_2F center,
                           const bool screenQuadrantAware,
                           const HWND window) const
{
    wil::com_ptr<IDWriteTextLayout> textLayout;
    winrt::check_hresult(
        dxgiAPI->writeFactory->CreateTextLayout(text,
                                                static_cast<uint32_t>(textLen),
                                                textFormat.get(),
                                                std::numeric_limits<float>::max(),
                                                std::numeric_limits<float>::max(),
                                                &textLayout));
    DWRITE_TEXT_METRICS textMetrics = {};
    winrt::check_hresult(textLayout->GetMetrics(&textMetrics));
    // Assumes text doesn't contain new lines
    const float lineHeight = textMetrics.height;
    textMetrics.width += lineHeight;
    textMetrics.height += lineHeight * .5f;
    winrt::check_hresult(textLayout->SetMaxWidth(textMetrics.width));
    winrt::check_hresult(textLayout->SetMaxHeight(textMetrics.height));

    D2D1_RECT_F textRect{ .left = center.x - textMetrics.width / 2.f,
                          .top = center.y - textMetrics.height / 2.f,
                          .right = center.x + textMetrics.width / 2.f,
                          .bottom = center.y + textMetrics.height / 2.f };

    const float SHADOW_OFFSET = consts::SHADOW_OFFSET * dpiScale;
    if (screenQuadrantAware)
    {
        bool cursorInLeftScreenHalf = false;
        bool cursorInTopScreenHalf = false;
        DetermineScreenQuadrant(window,
                                static_cast<long>(center.x),
                                static_cast<long>(center.y),
                                cursorInLeftScreenHalf,
                                cursorInTopScreenHalf);
        float textQuadrantOffsetX = textMetrics.width / 2.f + SHADOW_OFFSET;
        float textQuadrantOffsetY = textMetrics.height / 2.f + SHADOW_OFFSET;
        if (!cursorInLeftScreenHalf)
            textQuadrantOffsetX *= -1.f;
        if (!cursorInTopScreenHalf)
            textQuadrantOffsetY *= -1.f;
        textRect.left += textQuadrantOffsetX;
        textRect.right += textQuadrantOffsetX;
        textRect.top += textQuadrantOffsetY;
        textRect.bottom += textQuadrantOffsetY;
    }

    // Draw shadow
    bitmapRt->BeginDraw();
    bitmapRt->Clear(D2D1::ColorF(0.f, 0.f, 0.f, 0.f));
    D2D1_ROUNDED_RECT textBoxRect;
    textBoxRect.radiusX = textBoxRect.radiusY = consts::TEXT_BOX_CORNER_RADIUS * dpiScale;
    textBoxRect.rect.bottom = textRect.bottom;
    textBoxRect.rect.top = textRect.top;
    textBoxRect.rect.left = textRect.left;
    textBoxRect.rect.right = textRect.right;
    bitmapRt->FillRoundedRectangle(textBoxRect, solidBrushes[Brush::border].get());
    bitmapRt->EndDraw();

    wil::com_ptr<ID2D1Bitmap> rtBitmap;
    bitmapRt->GetBitmap(&rtBitmap);

    shadowEffect->SetInput(0, rtBitmap.get());
    const auto shadowMatrix = D2D1::Matrix3x2F::Translation(SHADOW_OFFSET, SHADOW_OFFSET);
    winrt::check_hresult(affineTransformEffect->SetValue(D2D1_2DAFFINETRANSFORM_PROP_TRANSFORM_MATRIX,
                                                         shadowMatrix));
    auto deviceContext = dxgiWindowState.rt.as<ID2D1DeviceContext>();
    deviceContext->DrawImage(affineTransformEffect.get(), D2D1_INTERPOLATION_MODE_LINEAR);

    // Draw text box border rectangle
    dxgiWindowState.rt->DrawRoundedRectangle(textBoxRect, solidBrushes[Brush::border].get());
    const float TEXT_BOX_PADDING = 1.f * dpiScale;
    textBoxRect.rect.bottom -= TEXT_BOX_PADDING;
    textBoxRect.rect.top += TEXT_BOX_PADDING;
    textBoxRect.rect.left += TEXT_BOX_PADDING;
    textBoxRect.rect.right -= TEXT_BOX_PADDING;

    // Draw text & its box
    dxgiWindowState.rt->FillRoundedRectangle(textBoxRect, solidBrushes[Brush::background].get());

    if (halfOpaqueSymbolPos.has_value())
    {
        DWRITE_TEXT_RANGE textRange = { static_cast<uint32_t>(*halfOpaqueSymbolPos), 2 };
        auto opacityEffect = winrt::make_self<OpacityEffect>();
        opacityEffect->alpha = consts::CROSS_OPACITY;
        winrt::check_hresult(textLayout->SetDrawingEffect(opacityEffect.get(), textRange));
    }
    winrt::check_hresult(textLayout->Draw(nullptr, textRenderer.get(), textRect.left, textRect.top));
}

void D2DState::ToggleAliasedLinesMode(const bool enabled) const
{
    if (enabled)
    {
        // Draw lines in the middle of a pixel to avoid bleeding, since [0,0] pixel is
        // a rectangle filled from (0,0) to (1,1) and the lines use thickness = 1.
        dxgiWindowState.rt->SetTransform(D2D1::Matrix3x2F::Translation(.5f, .5f));
        dxgiWindowState.rt->SetAntialiasMode(D2D1_ANTIALIAS_MODE_ALIASED);
    }
    else
    {
        dxgiWindowState.rt->SetTransform(D2D1::Matrix3x2F::Identity());
        dxgiWindowState.rt->SetAntialiasMode(D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
    }
}
