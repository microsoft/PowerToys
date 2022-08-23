#include "pch.h"

#include "constants.h"
#include "D2DState.h"

#include <common/Display/dpi_aware.h>

namespace
{
    void DetermineScreenQuadrant(HWND window, long x, long y, bool& inLeftHalf, bool& inTopHalf)
    {
        RECT windowRect{};
        GetWindowRect(window, &windowRect);
        const long w = windowRect.right - windowRect.left;
        const long h = windowRect.bottom - windowRect.top;
        inLeftHalf = x < w / 2;
        inTopHalf = y < h / 2;
    }
}

D2DState::D2DState(HWND overlayWindow, std::vector<D2D1::ColorF> solidBrushesColors)
{
    RECT clientRect = {};

    winrt::check_bool(GetClientRect(overlayWindow, &clientRect));
    winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &d2dFactory));

    winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), writeFactory.put_unknown()));

    auto renderTargetProperties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED),
        DPIAware::DEFAULT_DPI,
        DPIAware::DEFAULT_DPI);

    auto renderTargetSize = D2D1::SizeU(clientRect.right - clientRect.left, clientRect.bottom - clientRect.top);
    auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(overlayWindow, renderTargetSize);

    winrt::check_hresult(d2dFactory->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, &rt));
    winrt::check_hresult(rt->CreateCompatibleRenderTarget(&bitmapRt));

    unsigned dpi = DPIAware::DEFAULT_DPI;
    DPIAware::GetScreenDPIForWindow(overlayWindow, dpi);
    dpiScale = dpi / static_cast<float>(DPIAware::DEFAULT_DPI);

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

    auto deviceContext = rt.query<ID2D1DeviceContext>();
    winrt::check_hresult(deviceContext->CreateEffect(CLSID_D2D1Shadow, &shadowEffect));
    winrt::check_hresult(shadowEffect->SetValue(D2D1_SHADOW_PROP_BLUR_STANDARD_DEVIATION, consts::SHADOW_RADIUS));
    winrt::check_hresult(shadowEffect->SetValue(D2D1_SHADOW_PROP_COLOR, D2D1::ColorF(0.f, 0.f, 0.f, consts::SHADOW_OPACITY)));

    winrt::check_hresult(deviceContext->CreateEffect(CLSID_D2D12DAffineTransform, &affineTransformEffect));
    affineTransformEffect->SetInputEffect(0, shadowEffect.get());
}

void D2DState::DrawTextBox(const wchar_t* text,
                           const uint32_t textLen,
                           const float cornerX,
                           const float cornerY,
                           const bool screenQuadrantAware,
                           HWND window) const
{
    wil::com_ptr<IDWriteTextLayout> textLayout;
    winrt::check_hresult(writeFactory->CreateTextLayout(text, textLen, textFormat.get(), 1000.f, 1000.f, &textLayout));
    DWRITE_TEXT_METRICS metrics = {};
    textLayout->GetMetrics(&metrics);

    bool cursorInLeftScreenHalf = false;
    bool cursorInTopScreenHalf = false;
    DetermineScreenQuadrant(window,
                            static_cast<long>(cornerX),
                            static_cast<long>(cornerY),
                            cursorInLeftScreenHalf,
                            cursorInTopScreenHalf);
    const float TEXT_BOX_MARGIN = 1.25f;
    const float textBoxWidth = metrics.width * TEXT_BOX_MARGIN;
    const float textBoxHeight = metrics.height * TEXT_BOX_MARGIN;

    const float TEXT_BOX_PADDING = 1.f * dpiScale;
    const float TEXT_BOX_OFFSET_AMOUNT_X = textBoxWidth * dpiScale;
    const float TEXT_BOX_OFFSET_AMOUNT_Y = textBoxWidth * dpiScale;
    const float TEXT_BOX_OFFSET_X = cursorInLeftScreenHalf ? TEXT_BOX_OFFSET_AMOUNT_X : -TEXT_BOX_OFFSET_AMOUNT_X;
    const float TEXT_BOX_OFFSET_Y = cursorInTopScreenHalf ? TEXT_BOX_OFFSET_AMOUNT_Y : -TEXT_BOX_OFFSET_AMOUNT_Y;

    D2D1_RECT_F textRect{ .left = cornerX - textBoxWidth / 2.f,
                          .top = cornerY - textBoxHeight / 2.f,
                          .right = cornerX + textBoxWidth / 2.f,
                          .bottom = cornerY + textBoxHeight / 2.f };
    if (screenQuadrantAware)
    {
        textRect.left += TEXT_BOX_OFFSET_X;
        textRect.right += TEXT_BOX_OFFSET_X;
        textRect.top += TEXT_BOX_OFFSET_Y;
        textRect.bottom += TEXT_BOX_OFFSET_Y;
    }

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
    affineTransformEffect->SetValue(D2D1_2DAFFINETRANSFORM_PROP_TRANSFORM_MATRIX,
                                    D2D1::Matrix3x2F::Translation(consts::SHADOW_OFFSET * dpiScale, consts::SHADOW_OFFSET * dpiScale));

    auto deviceContext = rt.query<ID2D1DeviceContext>();
    deviceContext->DrawImage(affineTransformEffect.get(), D2D1_INTERPOLATION_MODE_LINEAR);

    rt->DrawRoundedRectangle(textBoxRect, solidBrushes[Brush::border].get());

    textBoxRect.rect.bottom -= TEXT_BOX_PADDING;
    textBoxRect.rect.top += TEXT_BOX_PADDING;
    textBoxRect.rect.left += TEXT_BOX_PADDING;
    textBoxRect.rect.right -= TEXT_BOX_PADDING;
    rt->FillRoundedRectangle(textBoxRect, solidBrushes[Brush::background].get());

    rt->DrawTextW(text, textLen, textFormat.get(), textRect, solidBrushes[Brush::foreground].get(), D2D1_DRAW_TEXT_OPTIONS_NONE);
}
