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

    unsigned dpi = DPIAware::DEFAULT_DPI;
    DPIAware::GetScreenDPIForWindow(overlayWindow, dpi);
    dpiScale = dpi / static_cast<float>(DPIAware::DEFAULT_DPI);

    winrt::check_hresult(writeFactory->CreateTextFormat(L"Segoe UI Variable Text",
                                                        nullptr,
                                                        DWRITE_FONT_WEIGHT_NORMAL,
                                                        DWRITE_FONT_STYLE_NORMAL,
                                                        DWRITE_FONT_STRETCH_NORMAL,
                                                        konst::FONT_SIZE * dpiScale,
                                                        L"en-US",
                                                        &textFormat));
    winrt::check_hresult(textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));
    winrt::check_hresult(textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER));

    solidBrushes.resize(solidBrushesColors.size());
    for (size_t i = 0; i < solidBrushes.size(); ++i)
    {
        winrt::check_hresult(rt->CreateSolidColorBrush(solidBrushesColors[i], &solidBrushes[i]));
    }
}

void D2DState::DrawTextBox(const wchar_t* text, uint32_t textLen, const float cornerX, const float cornerY, HWND window) const
{
    bool cursorInLeftScreenHalf = false;
    bool cursorInTopScreenHalf = false;

    DetermineScreenQuadrant(window,
                            static_cast<long>(cornerX),
                            static_cast<long>(cornerY),
                            cursorInLeftScreenHalf,
                            cursorInTopScreenHalf);

    // TODO: determine text bounding box instead of hard-coding it
    const float TEXT_BOX_WIDTH = 80.f * dpiScale;
    const float TEXT_BOX_HEIGHT = 32.f * dpiScale;

    const float TEXT_BOX_PADDING = 1.f * dpiScale;
    const float TEXT_BOX_OFFSET_AMOUNT_X = TEXT_BOX_WIDTH * dpiScale;
    const float TEXT_BOX_OFFSET_AMOUNT_Y = TEXT_BOX_WIDTH * dpiScale;
    const float TEXT_BOX_OFFSET_X = cursorInLeftScreenHalf ? TEXT_BOX_OFFSET_AMOUNT_X : -TEXT_BOX_OFFSET_AMOUNT_X;
    const float TEXT_BOX_OFFSET_Y = cursorInTopScreenHalf ? TEXT_BOX_OFFSET_AMOUNT_Y : -TEXT_BOX_OFFSET_AMOUNT_Y;

    D2D1_RECT_F textRect{ .left = cornerX - TEXT_BOX_WIDTH / 2.f + TEXT_BOX_OFFSET_X,
                          .top = cornerY - TEXT_BOX_HEIGHT / 2.f + TEXT_BOX_OFFSET_Y,
                          .right = cornerX + TEXT_BOX_WIDTH / 2.f + TEXT_BOX_OFFSET_X,
                          .bottom = cornerY + TEXT_BOX_HEIGHT / 2.f + TEXT_BOX_OFFSET_Y };

    D2D1_ROUNDED_RECT textBoxRect;
    textBoxRect.radiusX = textBoxRect.radiusY = konst::TEXT_BOX_CORNER_RADIUS * dpiScale;
    textBoxRect.rect.bottom = textRect.bottom - TEXT_BOX_PADDING;
    textBoxRect.rect.top = textRect.top + TEXT_BOX_PADDING;
    textBoxRect.rect.left = textRect.left - TEXT_BOX_PADDING;
    textBoxRect.rect.right = textRect.right + TEXT_BOX_PADDING;

    rt->DrawRoundedRectangle(textBoxRect, solidBrushes[Brush::border].get());
    rt->FillRoundedRectangle(textBoxRect, solidBrushes[Brush::background].get());
    rt->DrawTextW(text, textLen, textFormat.get(), textRect, solidBrushes[Brush::foreground].get(), D2D1_DRAW_TEXT_OPTIONS_NO_SNAP);
}
