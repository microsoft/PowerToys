#include "pch.h"

#include "OverlayUIDrawing.h"

#include <common/Display/dpi_aware.h>
#include <common/Display/monitors.h>
#include <common/utils/window.h>
#include <common/logger/logger.h>
#include <common/Themes/windows_colors.h>

namespace NonLocalizable
{
    const wchar_t MeasureToolOverlayWindowName[] = L"PowerToys.MeasureToolOverlayWindow";
    const wchar_t BoundsToolOverlayWindowName[] = L"PowerToys.BoundsToolOverlayWindow";
}

//#define DEBUG_OVERLAY

static wchar_t measureStringBuf[32] = {};
std::atomic_bool stopUILoop = false;
D2D1::ColorF foreground = D2D1::ColorF::Black;
D2D1::ColorF background = D2D1::ColorF(0.96f, 0.96f, 0.96f, 1.0f);
D2D1::ColorF border = D2D1::ColorF(0.44f, 0.44f, 0.44f, 0.4f);

void SetClipBoardToText(const std::wstring_view text)
{
    if (!OpenClipboard(nullptr))
    {
        return;
    }

    wil::unique_hglobal handle{ GlobalAlloc(GMEM_MOVEABLE, static_cast<size_t>((text.length() + 1) * sizeof(wchar_t))) };
    if (!handle)
    {
        CloseClipboard();
        return;
    }

    if (wchar_t* bufPtr = static_cast<wchar_t*>(GlobalLock(handle.get())); bufPtr != nullptr)
    {
        text.copy(bufPtr, text.length());
        GlobalUnlock(handle.get());
    }

    EmptyClipboard();
    SetClipboardData(CF_UNICODETEXT, handle.get());
    CloseClipboard();
}



    LRESULT CALLBACK measureToolWndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    const auto closeWindow = [&] {
        stopUILoop = true;
        PostMessageW(window, WM_CLOSE, {}, {});
    };

    switch (message)
    {
    case WM_KEYUP:
        if (wparam == VK_ESCAPE)
        {
            closeWindow();
        }
        break;
    case WM_LBUTTONUP:
        closeWindow();
        break;
    case WM_RBUTTONUP:
        SetClipBoardToText(measureStringBuf);
        break;
    case WM_ERASEBKGND:
        return 1;
    case WM_CREATE:
#if !defined(DEBUG_OVERLAY)
        for (; ShowCursor(false) > 0;)
            ;
#endif
        [[fallthrough]];
    default:
        return DefWindowProcW(window, message, wparam, lparam);
    }

    return 0;
}

LRESULT CALLBACK boundsToolWndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    static BoundsToolState* toolState = nullptr;

    const auto closeWindow = [&] {
        stopUILoop = true;
        PostMessageW(window, WM_CLOSE, {}, {});
    };

    switch (message)
    {
    case WM_CREATE:
        toolState = reinterpret_cast<BoundsToolState*>(reinterpret_cast<CREATESTRUCT*>(lparam)->lpCreateParams);
        break;
    case WM_KEYUP:
        if (wparam == VK_ESCAPE)
        {
            closeWindow();
        }
        break;
    case WM_LBUTTONDOWN:
    {
        POINT cursorPos = {};
        GetCursorPos(&cursorPos);
        ScreenToClient(window, &cursorPos);

        D2D_POINT_2F newRegionStart = { .x = static_cast<float>(cursorPos.x), .y = static_cast<float>(cursorPos.y) };
        toolState->currentRegionStart = newRegionStart;
        break;
    }
    case WM_LBUTTONUP:
        if (toolState->currentRegionStart.has_value())
        {
            POINT cursorPos = {};
            GetCursorPos(&cursorPos);
            ScreenToClient(window, &cursorPos);
            D2D_POINT_2F newRegionEnd = { .x = static_cast<float>(cursorPos.x), .y = static_cast<float>(cursorPos.y) };
            toolState->currentRegionStart = std::nullopt;
            SetClipBoardToText(measureStringBuf);
        }
        break;
    case WM_RBUTTONUP:
        closeWindow();
        break;
    case WM_ERASEBKGND:
        return 1;
    default:
        return DefWindowProcW(window, message, wparam, lparam);
    }

    return 0;
}

void CreateOverlayWindowClasses()
{
    WNDCLASSEXW wcex{ .cbSize = sizeof(WNDCLASSEX), .hInstance = GetModuleHandleW(nullptr) };
    wcex.lpfnWndProc = measureToolWndProc;
    wcex.lpszClassName = NonLocalizable::MeasureToolOverlayWindowName;
    RegisterClassExW(&wcex);

    wcex.lpfnWndProc = boundsToolWndProc;
    wcex.lpszClassName = NonLocalizable::BoundsToolOverlayWindowName;
    wcex.hCursor = LoadCursorW(nullptr, IDC_CROSS);

    RegisterClassExW(&wcex);
}

HWND CreateOverlayUIWindow(HMONITOR monitor, const wchar_t* windowClass, void* extraParam = nullptr)
{
    static std::once_flag windowClassesCreatedFlag;
    std::call_once(windowClassesCreatedFlag, CreateOverlayWindowClasses);

    int left = {}, top = {};
    int width = {}, height = {};

    MONITORINFO monitorInfo = { .cbSize = sizeof(monitorInfo) };
    if (GetMonitorInfoW(monitor, &monitorInfo))
    {
        left = monitorInfo.rcWork.left;
        top = monitorInfo.rcWork.top;
        width = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
        height = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;
    }

    HWND window{ CreateWindowExW(WS_EX_TOOLWINDOW,
                                 windowClass,
                                 L"PowerToys.MeasureToolOverlay",
                                 WS_POPUP,
                                 left,
                                 top,
                                 width,
                                 height,
                                 nullptr,
                                 nullptr,
                                 GetModuleHandleW(nullptr),
                                 extraParam) };
    winrt::check_bool(window);
    ShowWindow(window, SW_SHOWNORMAL);
#if !defined(DEBUG_OVERLAY)
    SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
#else
    (void)window;
#endif

    int const pos = -GetSystemMetrics(SM_CXVIRTUALSCREEN) - 8;
    if (wil::unique_hrgn hrgn{ CreateRectRgn(pos, 0, (pos + 1), 1) })
    {
        DWM_BLURBEHIND bh = { DWM_BB_ENABLE | DWM_BB_BLURREGION, TRUE, hrgn.get(), FALSE };
        DwmEnableBlurBehindWindow(window, &bh);
    }

    return window;
}

struct D2DState
{
    wil::com_ptr<ID2D1Factory> d2dFactory;
    wil::com_ptr<IDWriteFactory> writeFactory;
    wil::com_ptr<ID2D1HwndRenderTarget> rt;
    wil::com_ptr<IDWriteTextFormat> textFormat;
    std::vector<wil::com_ptr<ID2D1SolidColorBrush>> solidBrushes;
    float dpiScale = 1.f;
    D2DState(HWND overlayWindow, std::vector<D2D1::ColorF> solidBrushesColors)
    {
        RECT clientRect = {};

        winrt::check_bool(GetClientRect(overlayWindow, &clientRect));
        winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &d2dFactory));

        winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), writeFactory.put_unknown()));

        auto renderTargetProperties = D2D1::RenderTargetProperties(
            D2D1_RENDER_TARGET_TYPE_DEFAULT,
            D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED),
            96.f,
            96.f);

        auto renderTargetSize = D2D1::SizeU(clientRect.right - clientRect.left, clientRect.bottom - clientRect.top);
        auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(overlayWindow, renderTargetSize);

        winrt::check_hresult(d2dFactory->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, &rt));

        unsigned dpi = DPIAware::DEFAULT_DPI;
        DPIAware::GetScreenDPIForWindow(overlayWindow, dpi);
        dpiScale = dpi / static_cast<float>(DPIAware::DEFAULT_DPI);

        constexpr float FONT_SIZE = 14.f;
        // TO DO: I'm not sure if this 'Segoe UI Variable Text' somehow magically falls back to Segoe UI on Windows 10 (like it does in WinUI.. @JAIME, can you check ?
        winrt::check_hresult(writeFactory->CreateTextFormat(L"Segoe UI Variable Text", nullptr, DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, FONT_SIZE * dpiScale, L"en-US", &textFormat));
        winrt::check_hresult(textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));
        winrt::check_hresult(textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER));

        solidBrushes.resize(solidBrushesColors.size());
        for (size_t i = 0; i < solidBrushes.size(); ++i)
        {
            winrt::check_hresult(rt->CreateSolidColorBrush(solidBrushesColors[i], &solidBrushes[i]));
        }
    }
};

enum Brush : size_t
{
    line,
    measureNumbers,
    measureBackground,
    measureBorder
};

void DetermineScreenQuadrant(HWND window, long x, long y, bool& inLeftHalf, bool& inTopHalf)
{
    RECT windowRect{};
    GetWindowRect(window, &windowRect);
    const long w = windowRect.right - windowRect.left;
    const long h = windowRect.bottom - windowRect.top;
    inLeftHalf = x < w / 2;
    inTopHalf = y < h / 2;
}

void DrawTextBox(const D2DState& d2dState,
                 const wchar_t* text,
                 uint32_t textLen,
                 const float cornerX,
                 const float cornerY,
                 HWND window)
{
    bool cursorInLeftScreenHalf = false;
    bool cursorInTopScreenHalf = false;

    DetermineScreenQuadrant(window,
                            static_cast<long>(cornerX),
                            static_cast<long>(cornerY),
                            cursorInLeftScreenHalf,
                            cursorInTopScreenHalf);

    const float dpiScale = d2dState.dpiScale;

    constexpr float TEXT_BOX_CORNER_RADIUS = 4.f;
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
    textBoxRect.radiusX = textBoxRect.radiusY = TEXT_BOX_CORNER_RADIUS * dpiScale;
    textBoxRect.rect.bottom = textRect.bottom - TEXT_BOX_PADDING;
    textBoxRect.rect.top = textRect.top + TEXT_BOX_PADDING;
    textBoxRect.rect.left = textRect.left - TEXT_BOX_PADDING;
    textBoxRect.rect.right = textRect.right + TEXT_BOX_PADDING;

    d2dState.rt->DrawRoundedRectangle(textBoxRect, d2dState.solidBrushes[Brush::measureBorder].get());
    d2dState.rt->FillRoundedRectangle(textBoxRect, d2dState.solidBrushes[Brush::measureBackground].get());
    d2dState.rt->DrawTextW(text, textLen, d2dState.textFormat.get(), textRect, d2dState.solidBrushes[Brush::measureNumbers].get(), D2D1_DRAW_TEXT_OPTIONS_NO_SNAP);
}

void SetOverlayUIColors()
{
    if (WindowsColors::is_dark_mode())
    {
        foreground = D2D1::ColorF::White;
        background = D2D1::ColorF(0.17f, 0.17f, 0.17f, 1.0f);
        border = D2D1::ColorF(0.44f, 0.44f, 0.44f, 0.4f);
    }
}

void DrawBoundsToolOverlayUILoop(BoundsToolState& toolState, HWND overlayWindow)
{
    SetOverlayUIColors();

    D2DState d2dState{ overlayWindow, { toolState.lineColor, foreground, background, border } };
   

    while (!stopUILoop)
    {
        d2dState.rt->BeginDraw();

        d2dState.rt->Clear(D2D1::ColorF(1.f, 1.f, 1.f, 0.f));

        if (toolState.currentRegionStart.has_value())
        {
            POINT cursorPos = {};
            GetCursorPos(&cursorPos);
            ScreenToClient(overlayWindow, &cursorPos);

            D2D1_RECT_F rect{ .left = toolState.currentRegionStart->x,
                              .top = toolState.currentRegionStart->y,
                              .right = static_cast<float>(cursorPos.x),
                              .bottom = static_cast<float>(cursorPos.y) };
            d2dState.rt->DrawRectangle(rect, d2dState.solidBrushes[Brush::line].get());

            const uint32_t textLen = swprintf_s(measureStringBuf,
                                                L"%.0f x %.0f",
                                                std::abs(rect.right - rect.left),
                                                std::abs(rect.top - rect.bottom));
            DrawTextBox(d2dState,
                        measureStringBuf,
                        textLen,
                        toolState.currentRegionStart->x,
                        toolState.currentRegionStart->y,
                        overlayWindow);
        }

        d2dState.rt->EndDraw();

        d2dState.rt->Flush();
        InvalidateRect(overlayWindow, nullptr, true);
        run_message_loop(true);
    }
}

void DrawMeasureToolOverlayUILoop(MeasureToolState& toolState, HWND overlayWindow)
{
    bool drawHorizontalCrossLine = true;
    bool drawVerticalCrossLine = true;
    bool continuousCapture = false;
    MeasureToolState::Mode toolMode;

    D2D1::ColorF crossColor = D2D1::ColorF::OrangeRed;

    toolState.Access([&](MeasureToolState::State& s) {
        crossColor = s.crossColor;
        toolMode = s.mode;
        continuousCapture = s.continuousCapture;
        switch (s.mode)
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
    });

    SetOverlayUIColors();
    D2DState d2dState{ overlayWindow, { crossColor, foreground, background, border } };

    while (!stopUILoop)
    {
        d2dState.rt->BeginDraw();
        auto previousAliasingMode = d2dState.rt->GetAntialiasMode();
        d2dState.rt->SetAntialiasMode(D2D1_ANTIALIAS_MODE_ALIASED); // Anti-aliasing is creating artifacts. Aliasing is for drawing straight lines.
        d2dState.rt->Clear(D2D1::ColorF(1.f, 1.f, 1.f, 0.f));

        MeasureToolState::State mts;
        toolState.Access([&mts](MeasureToolState::State& state) {
            mts = state;
        });

        const float CROSS_THICKNESS = 1.f;
        const float FEET_LENGTH = 5.f;

        if (drawHorizontalCrossLine)
        {
            d2dState.rt->DrawLine(mts.cross.hLineStart, mts.cross.hLineEnd, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
            if (!continuousCapture)
            {
                //draw line feet
                D2D_POINT_2F start_feet_top_half_start = { mts.cross.hLineStart.x + CROSS_THICKNESS / 2, mts.cross.hLineStart.y - CROSS_THICKNESS / 2 };
                D2D_POINT_2F start_feet_top_half_end = { mts.cross.hLineStart.x + CROSS_THICKNESS / 2, mts.cross.hLineStart.y - FEET_LENGTH };
                d2dState.rt->DrawLine(start_feet_top_half_start, start_feet_top_half_end, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
                D2D_POINT_2F start_feet_bottom_half_start = { mts.cross.hLineStart.x + CROSS_THICKNESS / 2, mts.cross.hLineStart.y + CROSS_THICKNESS / 2 };
                D2D_POINT_2F start_feet_bottom_half_end = { mts.cross.hLineStart.x + CROSS_THICKNESS / 2, mts.cross.hLineStart.y + FEET_LENGTH };
                d2dState.rt->DrawLine(start_feet_bottom_half_start, start_feet_bottom_half_end, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
                D2D_POINT_2F end_feet_start_top_half_start = { mts.cross.hLineEnd.x - CROSS_THICKNESS / 2, mts.cross.hLineEnd.y - CROSS_THICKNESS / 2 };
                D2D_POINT_2F end_feet_end_top_half_end = { mts.cross.hLineEnd.x - CROSS_THICKNESS / 2, mts.cross.hLineEnd.y - FEET_LENGTH };
                d2dState.rt->DrawLine(end_feet_start_top_half_start, end_feet_end_top_half_end, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
                D2D_POINT_2F end_feet_start_bottom_half_start = { mts.cross.hLineEnd.x - CROSS_THICKNESS / 2, mts.cross.hLineEnd.y + CROSS_THICKNESS / 2 };
                D2D_POINT_2F end_feet_end_bottom_half_end = { mts.cross.hLineEnd.x - CROSS_THICKNESS / 2, mts.cross.hLineEnd.y + FEET_LENGTH };
                d2dState.rt->DrawLine(end_feet_start_bottom_half_start, end_feet_end_bottom_half_end, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
            }
        }

        if (drawVerticalCrossLine)
        {
            d2dState.rt->DrawLine(mts.cross.vLineStart, mts.cross.vLineEnd, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
            //draw line feet
            if (!continuousCapture)
            {
                D2D_POINT_2F start_feet_left_half_start = { mts.cross.vLineStart.x - CROSS_THICKNESS / 2, mts.cross.vLineStart.y + CROSS_THICKNESS / 2 };
                D2D_POINT_2F start_feet_left_half_end = { mts.cross.vLineStart.x - FEET_LENGTH, mts.cross.vLineStart.y + CROSS_THICKNESS / 2 };
                d2dState.rt->DrawLine(start_feet_left_half_start, start_feet_left_half_end, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
                D2D_POINT_2F start_feet_right_half_start = { mts.cross.vLineStart.x + CROSS_THICKNESS / 2, mts.cross.vLineStart.y + CROSS_THICKNESS / 2 };
                D2D_POINT_2F start_feet_right_half_end = { mts.cross.vLineStart.x + FEET_LENGTH, mts.cross.vLineStart.y + CROSS_THICKNESS / 2 };
                d2dState.rt->DrawLine(start_feet_right_half_start, start_feet_right_half_end, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
                D2D_POINT_2F end_feet_left_half_start = { mts.cross.vLineEnd.x - CROSS_THICKNESS / 2, mts.cross.vLineEnd.y - CROSS_THICKNESS / 2 };
                D2D_POINT_2F end_feet_left_half_end = { mts.cross.vLineEnd.x - FEET_LENGTH, mts.cross.vLineEnd.y - CROSS_THICKNESS / 2 };
                d2dState.rt->DrawLine(end_feet_left_half_start, end_feet_left_half_end, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
                D2D_POINT_2F end_feet_right_half_start = { mts.cross.vLineEnd.x + CROSS_THICKNESS / 2, mts.cross.vLineEnd.y - CROSS_THICKNESS / 2 };
                D2D_POINT_2F end_feet_right_half_end = { mts.cross.vLineEnd.x + FEET_LENGTH, mts.cross.vLineEnd.y - CROSS_THICKNESS / 2 };
                d2dState.rt->DrawLine(end_feet_right_half_start, end_feet_right_half_end, d2dState.solidBrushes[Brush::line].get(), CROSS_THICKNESS);
            }
        }

        // After drawing the lines, restore anti aliasing to draw the measurement tooltip.
        d2dState.rt->SetAntialiasMode(previousAliasingMode);

        const float hMeasure = mts.cross.hLineEnd.x - mts.cross.hLineStart.x;
        const float vMeasure = mts.cross.vLineEnd.y - mts.cross.vLineStart.y;
        uint32_t measureStringBufLen = 0;

        switch (toolMode)
        {
        case MeasureToolState::Mode::Cross:
            measureStringBufLen = swprintf_s(measureStringBuf,
                                             L"%.0f x %.0f",
                                             hMeasure,
                                             vMeasure);
            break;
        case MeasureToolState::Mode::Vertical:
            measureStringBufLen = swprintf_s(measureStringBuf,
                                             L"%.0f",
                                             vMeasure);
            break;
        case MeasureToolState::Mode::Horizontal:
            measureStringBufLen = swprintf_s(measureStringBuf,
                                             L"%.0f",
                                             hMeasure);
            break;
        }

        DrawTextBox(d2dState,
                    measureStringBuf,
                    measureStringBufLen,
                    static_cast<float>(mts.cursorPos.x),
                    static_cast<float>(mts.cursorPos.y),
                    overlayWindow);

        d2dState.rt->EndDraw();

        // d2dState.rt->Flush(); EndDraw should flush already
        InvalidateRect(overlayWindow, nullptr, true);
        run_message_loop(true);
    }

    toolState.Access([](MeasureToolState::State& state) {
        state.stopCapturing = true;
    });
}

HWND LaunchOverlayUI(MeasureToolState& measureToolState, HMONITOR monitor)
{
    stopUILoop = false;

    wil::shared_event windowCreatedEvent(wil::EventOptions::ManualReset);

    HWND window = {};
    SpawnLoggedThread([&measureToolState, monitor, &window, windowCreatedEvent] {
        window = CreateOverlayUIWindow(monitor, NonLocalizable::MeasureToolOverlayWindowName);
        windowCreatedEvent.SetEvent();
        DrawMeasureToolOverlayUILoop(measureToolState, window);
    }, L"Launch measure tool OverlayUI");

    windowCreatedEvent.wait();

    return window;
}

HWND LaunchOverlayUI(BoundsToolState& boundsToolState, HMONITOR monitor)
{
    stopUILoop = false;

    wil::shared_event windowCreatedEvent(wil::EventOptions::ManualReset);

    HWND window = {};
    SpawnLoggedThread([&boundsToolState, monitor, &window, windowCreatedEvent] {
        window = CreateOverlayUIWindow(monitor, NonLocalizable::BoundsToolOverlayWindowName, &boundsToolState);
        windowCreatedEvent.SetEvent();
        DrawBoundsToolOverlayUILoop(boundsToolState, window);
    }, L"Launch bounds tool OverlayUI");

    windowCreatedEvent.wait();

    return window;
}

