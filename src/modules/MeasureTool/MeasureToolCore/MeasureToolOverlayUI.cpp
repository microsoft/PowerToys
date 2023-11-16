#include "pch.h"

#include "BGRATextureView.h"
#include "Clipboard.h"
#include "CoordinateSystemConversion.h"
#include "constants.h"
#include "MeasureToolOverlayUI.h"

#include <common/utils/window.h>

namespace
{
    inline std::pair<D2D_POINT_2F, D2D_POINT_2F> ComputeCrossFeetLine(D2D_POINT_2F center, const bool horizontal)
    {
        D2D_POINT_2F start = center, end = center;
        // Computing in this way to achieve pixel-perfect axial symmetry of aliased D2D lines
        if (horizontal)
        {
            start.x -= consts::FEET_HALF_LENGTH;
            end.x += consts::FEET_HALF_LENGTH + 1.f;
        }
        else
        {
            start.y -= consts::FEET_HALF_LENGTH;
            end.y += consts::FEET_HALF_LENGTH + 1.f;
        }

        return { start, end };
    }
}

winrt::com_ptr<ID2D1Bitmap> ConvertID3D11Texture2DToD2D1Bitmap(winrt::com_ptr<ID2D1RenderTarget> rt,
                                                               const MappedTextureView* capturedScreenTexture)
{
    D2D1_BITMAP_PROPERTIES props = { .pixelFormat = rt->GetPixelFormat() };
    rt->GetDpi(&props.dpiX, &props.dpiY);
    const auto sizeF = rt->GetSize();
    winrt::com_ptr<ID2D1Bitmap> bitmap;
    auto hr = rt->CreateBitmap(D2D1::SizeU(static_cast<uint32_t>(capturedScreenTexture->view.width),
                                           static_cast<uint32_t>(capturedScreenTexture->view.height)),
                               capturedScreenTexture->view.pixels,
                               static_cast<uint32_t>(capturedScreenTexture->view.pitch * 4),
                               props,
                               bitmap.put());
    if (FAILED(hr))
        return nullptr;

    return bitmap;
}

LRESULT CALLBACK MeasureToolWndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_MOUSELEAVE:
    case WM_CURSOR_LEFT_MONITOR:
    {
        if (auto state = GetWindowParam<Serialized<MeasureToolState>*>(window))
        {
            state->Access([&](MeasureToolState& s) {
                s.perScreen[window].measuredEdges = {};
            });
        }
        break;
    }
    case WM_NCHITTEST:
        return HTCLIENT;
    case WM_CREATE:
    {
        auto state = GetWindowCreateParam<Serialized<MeasureToolState>*>(lparam);
        StoreWindowParam(window, state);

#if !defined(DEBUG_OVERLAY)
        for (; ShowCursor(false) >= 0;)
            ;
#endif
        break;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_KEYUP:
        if (wparam == VK_ESCAPE)
        {
            PostMessageW(window, WM_CLOSE, {}, {});
        }
        break;
    case WM_RBUTTONUP:
        PostMessageW(window, WM_CLOSE, {}, {});
        break;
    case WM_LBUTTONUP:
        if (auto state = GetWindowParam<Serialized<MeasureToolState>*>(window))
        {
            state->Read([](const MeasureToolState& s) { s.commonState->overlayBoxText.Read([](const OverlayBoxText& text) {
                                                            SetClipBoardToText(text.buffer.data());
                                                        }); });
        }
        PostMessageW(window, WM_CLOSE, {}, {});
        break;
    case WM_MOUSEWHEEL:
        if (auto state = GetWindowParam<Serialized<MeasureToolState>*>(window))
        {
            const int8_t step = static_cast<short>(HIWORD(wparam)) < 0 ? -consts::MOUSE_WHEEL_TOLERANCE_STEP : consts::MOUSE_WHEEL_TOLERANCE_STEP;
            state->Access([step](MeasureToolState& s) {
                int wideVal = s.global.pixelTolerance;
                wideVal += step;
                s.global.pixelTolerance = static_cast<uint8_t>(std::clamp(wideVal, 0, 255));
            });
        }
        break;
    }

    return DefWindowProcW(window, message, wparam, lparam);
}

void DrawMeasureToolTick(const CommonState& commonState,
                         Serialized<MeasureToolState>& toolState,
                         HWND window,
                         D2DState& d2dState)
{
    bool continuousCapture = {};
    bool drawFeetOnCross = {};
    bool drawHorizontalCrossLine = true;
    bool drawVerticalCrossLine = true;

    Measurement measuredEdges{};
    MeasureToolState::Mode mode = {};
    winrt::com_ptr<ID2D1Bitmap> backgroundBitmap;
    const MappedTextureView* backgroundTextureToConvert = nullptr;

    bool gotMeasurement = false;
    toolState.Read([&](const MeasureToolState& state) {
        continuousCapture = state.global.continuousCapture;
        drawFeetOnCross = state.global.drawFeetOnCross;
        mode = state.global.mode;
        if (auto it = state.perScreen.find(window); it != end(state.perScreen))
        {
            const auto& perScreen = it->second;
            if (!perScreen.measuredEdges)
            {
                return;
            }

            gotMeasurement = true;
            measuredEdges = *perScreen.measuredEdges;

            if (continuousCapture)
                return;

            if (perScreen.capturedScreenBitmap)
            {
                backgroundBitmap = perScreen.capturedScreenBitmap;
            }
            else if (perScreen.capturedScreenTexture)
            {
                backgroundTextureToConvert = perScreen.capturedScreenTexture;
            }
        }
    });

    if (!gotMeasurement)
        return;

    switch (mode)
    {
    case MeasureToolState::Mode::Cross:
        drawHorizontalCrossLine = true;
        drawVerticalCrossLine = true;
        break;
    case MeasureToolState::Mode::Vertical:
        drawHorizontalCrossLine = false;
        drawVerticalCrossLine = true;
        break;
    case MeasureToolState::Mode::Horizontal:
        drawHorizontalCrossLine = true;
        drawVerticalCrossLine = false;
        break;
    }

    if (!continuousCapture && !backgroundBitmap && backgroundTextureToConvert)
    {
        backgroundBitmap = ConvertID3D11Texture2DToD2D1Bitmap(d2dState.dxgiWindowState.rt, backgroundTextureToConvert);
        if (backgroundBitmap)
        {
            toolState.Access([&](MeasureToolState& state) {
                state.perScreen[window].capturedScreenTexture = {};
                state.perScreen[window].capturedScreenBitmap = backgroundBitmap;
            });
        }
    }

    if (continuousCapture || !backgroundBitmap)
        d2dState.dxgiWindowState.rt->Clear();

    const float hMeasure = measuredEdges.Width(Measurement::Unit::Pixel);
    const float vMeasure = measuredEdges.Height(Measurement::Unit::Pixel);

    if (!continuousCapture && backgroundBitmap)
    {
        d2dState.dxgiWindowState.rt->DrawBitmap(backgroundBitmap.get());
    }

    const auto cursorPos = convert::FromSystemToWindow(window, commonState.cursorPosSystemSpace);

    d2dState.ToggleAliasedLinesMode(true);
    if (drawHorizontalCrossLine)
    {
        const D2D_POINT_2F hLineStart{ .x = measuredEdges.rect.left, .y = static_cast<float>(cursorPos.y) };
        D2D_POINT_2F hLineEnd{ .x = hLineStart.x + hMeasure, .y = hLineStart.y };
        d2dState.dxgiWindowState.rt->DrawLine(hLineStart, hLineEnd, d2dState.solidBrushes[Brush::line].get());

        if (drawFeetOnCross)
        {
            // To fill all pixels which are close, we call DrawLine with end point one pixel too far, since
            // it doesn't get filled, i.e. end point of the range is excluded. However, we want to draw cross
            // feet *on* the last pixel row, so we must subtract 1px from the corresponding axis.
            hLineEnd.x -= 1.f;
            auto [left_start, left_end] = ComputeCrossFeetLine(hLineStart, false);
            auto [right_start, right_end] = ComputeCrossFeetLine(hLineEnd, false);
            d2dState.dxgiWindowState.rt->DrawLine(left_start, left_end, d2dState.solidBrushes[Brush::line].get());
            d2dState.dxgiWindowState.rt->DrawLine(right_start, right_end, d2dState.solidBrushes[Brush::line].get());
        }
    }

    if (drawVerticalCrossLine)
    {
        const D2D_POINT_2F vLineStart{ .x = static_cast<float>(cursorPos.x), .y = measuredEdges.rect.top };
        D2D_POINT_2F vLineEnd{ .x = vLineStart.x, .y = vLineStart.y + vMeasure };
        d2dState.dxgiWindowState.rt->DrawLine(vLineStart, vLineEnd, d2dState.solidBrushes[Brush::line].get());

        if (drawFeetOnCross)
        {
            vLineEnd.y -= 1.f;
            auto [top_start, top_end] = ComputeCrossFeetLine(vLineStart, true);
            auto [bottom_start, bottom_end] = ComputeCrossFeetLine(vLineEnd, true);
            d2dState.dxgiWindowState.rt->DrawLine(top_start, top_end, d2dState.solidBrushes[Brush::line].get());
            d2dState.dxgiWindowState.rt->DrawLine(bottom_start, bottom_end, d2dState.solidBrushes[Brush::line].get());
        }
    }

    d2dState.ToggleAliasedLinesMode(false);

    OverlayBoxText text;

    const auto [crossSymbolPos, measureStringBufLen] =
        measuredEdges.Print(text.buffer.data(),
                            text.buffer.size(),
                            drawHorizontalCrossLine,
                            drawVerticalCrossLine,
                            commonState.units);

    commonState.overlayBoxText.Access([&](OverlayBoxText& v) {
        v = text;
    });

    d2dState.DrawTextBox(text.buffer.data(),
                         measureStringBufLen,
                         crossSymbolPos,
                         D2D_POINT_2F{ static_cast<float>(cursorPos.x), static_cast<float>(cursorPos.y) },
                         true,
                         window);
}
