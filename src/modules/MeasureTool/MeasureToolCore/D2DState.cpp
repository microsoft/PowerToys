#include "pch.h"

#include "constants.h"
#include "D2DState.h"

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

D2DState::D2DState(const HWND overlayWindow, std::vector<D2D1::ColorF> solidBrushesColors)
{
    std::lock_guard guard{ gpuAccessLock };

    RECT clientRect = {};

    winrt::check_bool(GetClientRect(overlayWindow, &clientRect));
    winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &d2dFactory));

    // We should always use DPIAware::DEFAULT_DPI, since it's the correct thing to do in DPI-Aware mode
    auto renderTargetProperties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED),
        DPIAware::DEFAULT_DPI,
        DPIAware::DEFAULT_DPI,
        D2D1_RENDER_TARGET_USAGE_NONE,
        D2D1_FEATURE_LEVEL_DEFAULT);

    auto renderTargetSize = D2D1::SizeU(clientRect.right - clientRect.left, clientRect.bottom - clientRect.top);
    auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(overlayWindow, renderTargetSize);

    winrt::check_hresult(d2dFactory->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, &rt));
    winrt::check_hresult(rt->CreateCompatibleRenderTarget(&bitmapRt));

    unsigned dpi = DPIAware::DEFAULT_DPI;
    DPIAware::GetScreenDPIForWindow(overlayWindow, dpi);
    dpiScale = dpi / static_cast<float>(DPIAware::DEFAULT_DPI);

    winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), writeFactory.put_unknown()));
    winrt::check_hresult(writeFactory->CreateTextFormat(L"Segoe UI Variable Text",
                                                        nullptr,
                                                        DWRITE_FONT_WEIGHT_NORMAL,
                                                        DWRITE_FONT_STYLE_NORMAL,
                                                        DWRITE_FONT_STRETCH_NORMAL,
                                                        consts::FONT_SIZE * dpiScale,
                                                        L"en-US",
                                                        &textFormat));
    winrt::check_hresult(textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));
    winrt::check_hresult(textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER));
    winrt::check_hresult(textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP));

    solidBrushes.resize(solidBrushesColors.size());
    for (size_t i = 0; i < solidBrushes.size(); ++i)
    {
        winrt::check_hresult(rt->CreateSolidColorBrush(solidBrushesColors[i], &solidBrushes[i]));
    }

    const auto deviceContext = rt.query<ID2D1DeviceContext>();
    winrt::check_hresult(deviceContext->CreateEffect(CLSID_D2D1Shadow, &shadowEffect));
    winrt::check_hresult(shadowEffect->SetValue(D2D1_SHADOW_PROP_BLUR_STANDARD_DEVIATION, consts::SHADOW_RADIUS));
    winrt::check_hresult(shadowEffect->SetValue(D2D1_SHADOW_PROP_COLOR, D2D1::ColorF(0.f, 0.f, 0.f, consts::SHADOW_OPACITY)));

    winrt::check_hresult(deviceContext->CreateEffect(CLSID_D2D12DAffineTransform, &affineTransformEffect));
    affineTransformEffect->SetInputEffect(0, shadowEffect.get());

    textRenderer = winrt::make_self<PerGlyphOpacityTextRender>(d2dFactory, rt, solidBrushes[Brush::foreground]);
}

void D2DState::DrawTextBox(const wchar_t* text,
                           const uint32_t textLen,
                           const std::optional<size_t> halfOpaqueSymbolPos,
                           const float centerX,
                           const float centerY,
                           const bool screenQuadrantAware,
                           const HWND window) const
{
    wil::com_ptr<IDWriteTextLayout> textLayout;
    winrt::check_hresult(writeFactory->CreateTextLayout(text,
                                                        textLen,
                                                        textFormat.get(),
                                                        std::numeric_limits<float>::max(),
                                                        std::numeric_limits<float>::max(),
                                                        &textLayout));
    DWRITE_TEXT_METRICS textMetrics = {};
    winrt::check_hresult(textLayout->GetMetrics(&textMetrics));
    textMetrics.width *= consts::TEXT_BOX_MARGIN_COEFF;
    textMetrics.height *= consts::TEXT_BOX_MARGIN_COEFF;
    winrt::check_hresult(textLayout->SetMaxWidth(textMetrics.width));
    winrt::check_hresult(textLayout->SetMaxHeight(textMetrics.height));

    D2D1_RECT_F textRect{ .left = centerX - textMetrics.width / 2.f,
                          .top = centerY - textMetrics.height / 2.f,
                          .right = centerX + textMetrics.width / 2.f,
                          .bottom = centerY + textMetrics.height / 2.f };
    if (screenQuadrantAware)
    {
        bool cursorInLeftScreenHalf = false;
        bool cursorInTopScreenHalf = false;
        DetermineScreenQuadrant(window,
                                static_cast<long>(centerX),
                                static_cast<long>(centerY),
                                cursorInLeftScreenHalf,
                                cursorInTopScreenHalf);
        float textQuadrantOffsetX = textMetrics.width * dpiScale;
        float textQuadrantOffsetY = textMetrics.height * dpiScale;
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
    const auto shadowMatrix = D2D1::Matrix3x2F::Translation(consts::SHADOW_OFFSET * dpiScale,
                                                            consts::SHADOW_OFFSET * dpiScale);
    winrt::check_hresult(affineTransformEffect->SetValue(D2D1_2DAFFINETRANSFORM_PROP_TRANSFORM_MATRIX,
                                                         shadowMatrix));
    auto deviceContext = rt.query<ID2D1DeviceContext>();
    deviceContext->DrawImage(affineTransformEffect.get(), D2D1_INTERPOLATION_MODE_LINEAR);

    // Draw text box border rectangle
    rt->DrawRoundedRectangle(textBoxRect, solidBrushes[Brush::border].get());
    const float TEXT_BOX_PADDING = 1.f * dpiScale;
    textBoxRect.rect.bottom -= TEXT_BOX_PADDING;
    textBoxRect.rect.top += TEXT_BOX_PADDING;
    textBoxRect.rect.left += TEXT_BOX_PADDING;
    textBoxRect.rect.right -= TEXT_BOX_PADDING;

    // Draw text & its box
    rt->FillRoundedRectangle(textBoxRect, solidBrushes[Brush::background].get());

    if (halfOpaqueSymbolPos.has_value())
    {
        DWRITE_TEXT_RANGE textRange = { static_cast<uint32_t>(*halfOpaqueSymbolPos), 2 };
        auto opacityEffect = winrt::make_self<OpacityEffect>();
        opacityEffect->alpha = consts::CROSS_OPACITY;
        winrt::check_hresult(textLayout->SetDrawingEffect(opacityEffect.get(), textRange));
    }
    winrt::check_hresult(textLayout->Draw(nullptr, textRenderer.get(), textRect.left, textRect.top));
}
