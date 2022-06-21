#include "pch.h"

#include "OverlayUIDrawing.h"

#include <common/Display/dpi_aware.h>
#include <common/Display/monitors.h>

namespace NonLocalizable
{
    const wchar_t OverlayWindowClassName[] = L"PowerToys.MeasureToolOverlayWindow";
}

constexpr float TEXT_BOX_CORNER_RADIUS = 4.f;

//#define DEBUG_OVERLAY

static wchar_t measureStringBuf[32] = {};
std::atomic_bool shouldStop = false;

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

    wchar_t* bufPtr = static_cast<wchar_t*>(GlobalLock(handle.get()));
    text.copy(bufPtr, text.length());
    GlobalUnlock(handle.get());
    EmptyClipboard();
    SetClipboardData(CF_UNICODETEXT, handle.get());
    CloseClipboard();
}

LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    const auto closeWindow = [&] {
        shouldStop = true;
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

void CreateOverlayWindowClass()
{
    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = GetModuleHandleW(nullptr);
    wcex.lpszClassName = NonLocalizable::OverlayWindowClassName;
    RegisterClassExW(&wcex);
}

HWND CreateOverlayUIWindow(HMONITOR monitor)
{
    int left = 0, top = 0;
    int width = 1920, height = 1080;

    MONITORINFO monitorInfo = {};
    monitorInfo.cbSize = sizeof(monitorInfo);
    if (GetMonitorInfoW(monitor, &monitorInfo))
    {
        left = monitorInfo.rcWork.left;
        top = monitorInfo.rcWork.top;
        width = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
        height = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;
    }

    HWND window{ CreateWindowExW(WS_EX_TOOLWINDOW,
                                 NonLocalizable::OverlayWindowClassName,
                                 L"",
                                 WS_POPUP,
                                 left,
                                 top,
                                 width,
                                 height,
                                 nullptr,
                                 nullptr,
                                 GetModuleHandleW(nullptr),
                                 nullptr) };

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

int run_message_loop(bool until_idle);

void DrawOverlayUILoop(MeasureToolState& measureToolState, HWND overlayWindow)
{
    wil::com_ptr<ID2D1Factory> d2dFactory;
    winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &d2dFactory));

    wil::com_ptr<IDWriteFactory> writeFactory;
    winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), writeFactory.put_unknown()));

    RECT clientRect = {};

    if (!GetClientRect(overlayWindow, &clientRect))
    {
        return;
    }
    auto renderTargetProperties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED),
        96.f,
        96.f);

    auto renderTargetSize = D2D1::SizeU(clientRect.right - clientRect.left, clientRect.bottom - clientRect.top);
    auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(overlayWindow, renderTargetSize);

    wil::com_ptr<ID2D1HwndRenderTarget> rt;

    winrt::check_hresult(d2dFactory->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, &rt));

    wil::com_ptr<ID2D1SolidColorBrush> crossBrush;
    winrt::check_hresult(rt->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::OrangeRed), &crossBrush));

    wil::com_ptr<ID2D1SolidColorBrush> measureNumbersBrush;
    winrt::check_hresult(rt->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Black), &measureNumbersBrush));

    wil::com_ptr<ID2D1SolidColorBrush> measureBackgroundBrush;
    winrt::check_hresult(rt->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::WhiteSmoke), &measureBackgroundBrush));

    unsigned dpi = DPIAware::DEFAULT_DPI;
    DPIAware::GetScreenDPIForWindow(overlayWindow, dpi);
    const float dpiScale = dpi / static_cast<float>(DPIAware::DEFAULT_DPI);

    wil::com_ptr<IDWriteTextFormat> textFormat;
    winrt::check_hresult(writeFactory->CreateTextFormat(L"Consolas", nullptr, DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 15.f * dpiScale, L"en-US", &textFormat));
    winrt::check_hresult(textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));
    winrt::check_hresult(textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER));

    wil::com_ptr<ID2D1StrokeStyle> strokeStyle;
#if DOTTED
    winrt::check_hresult(d2dFactory->CreateStrokeStyle(
        D2D1::StrokeStyleProperties(
            D2D1_CAP_STYLE_FLAT,
            D2D1_CAP_STYLE_FLAT,
            D2D1_CAP_STYLE_ROUND,
            D2D1_LINE_JOIN_MITER,
            10.0f,
            D2D1_DASH_STYLE_DASH_DOT,
            0.0f),
        nullptr,
        0,
        strokeStyle.put()));
#endif

    bool drawHCrossLine = true;
    bool drawVCrossLine = true;
    MeasureToolState::Mode crossMode;
    measureToolState.Access([&](auto&& s) {
        crossMode = s.mode;
        switch (s.mode)
        {
        case MeasureToolState::Mode::Cross:
            drawHCrossLine = true;
            drawVCrossLine = true;
            break;
        case MeasureToolState::Mode::Vertical:
            drawHCrossLine = false;
            drawVCrossLine = true;
            break;
        case MeasureToolState::Mode::Horizontal:
            drawHCrossLine = true;
            drawVCrossLine = false;
            break;
        }
    });

    while (!shouldStop)
    {
        rt->BeginDraw();

        rt->Clear(D2D1::ColorF(1.f, 1.f, 1.f, 0.f));

        MeasureToolState::State mts;
        measureToolState.Access([&mts](MeasureToolState::State& state) {
            mts = state;
        });

        const float TEXT_BOX_WIDTH = 80.f * dpiScale;
        const float TEXT_BOX_HEIGHT = 10.f * dpiScale;
        const float CROSS_THICKNESS = 1.f * dpiScale;

        if (drawHCrossLine)
        {
            rt->DrawLine(mts.cross.hLineStart, mts.cross.hLineEnd, crossBrush.get(), CROSS_THICKNESS, strokeStyle.get());
        }

        if (drawVCrossLine)
        {
            rt->DrawLine(mts.cross.vLineStart, mts.cross.vLineEnd, crossBrush.get(), CROSS_THICKNESS, strokeStyle.get());
        }
        D2D1_RECT_F textRect;

        const float TEXT_BOX_PADDING = 16.f * dpiScale;
        const float TEXT_BOX_OFFSET_AMOUNT_X = TEXT_BOX_WIDTH * dpiScale;
        const float TEXT_BOX_OFFSET_AMOUNT_Y = TEXT_BOX_WIDTH * dpiScale;
        const float TEXT_BOX_OFFSET_X = mts.cursorInLeftScreenHalf ? TEXT_BOX_OFFSET_AMOUNT_X : -TEXT_BOX_OFFSET_AMOUNT_X;
        const float TEXT_BOX_OFFSET_Y = mts.cursorInTopScreenHalf ? TEXT_BOX_OFFSET_AMOUNT_Y : -TEXT_BOX_OFFSET_AMOUNT_Y;

        textRect.left = mts.cursorPos.x - TEXT_BOX_WIDTH / 2.f + TEXT_BOX_OFFSET_X;
        textRect.right = mts.cursorPos.x + TEXT_BOX_WIDTH / 2.f + TEXT_BOX_OFFSET_X;
        textRect.top = mts.cursorPos.y - TEXT_BOX_HEIGHT / 2.f + TEXT_BOX_OFFSET_Y;
        textRect.bottom = mts.cursorPos.y + TEXT_BOX_HEIGHT / 2.f + TEXT_BOX_OFFSET_Y;

        const float hMeasure = mts.cross.hLineEnd.x - mts.cross.hLineStart.x;
        const float vMeasure = mts.cross.vLineEnd.y - mts.cross.vLineStart.y;
        uint32_t measureStringBufLen = 0;

        switch (crossMode)
        {
        case MeasureToolState::Mode::Cross:
            measureStringBufLen = swprintf_s(measureStringBuf,
                                             L"%.0fx%.0f",
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

        D2D1_ROUNDED_RECT textBoxRect;
        textBoxRect.radiusX = textBoxRect.radiusY = TEXT_BOX_CORNER_RADIUS * dpiScale;
        textBoxRect.rect.bottom = textRect.bottom - TEXT_BOX_PADDING;
        textBoxRect.rect.top = textRect.top + TEXT_BOX_PADDING;
        textBoxRect.rect.left = textRect.left - TEXT_BOX_PADDING;
        textBoxRect.rect.right = textRect.right + TEXT_BOX_PADDING;

        rt->DrawRoundedRectangle(textBoxRect, measureNumbersBrush.get());
        rt->FillRoundedRectangle(textBoxRect, measureBackgroundBrush.get());

        rt->DrawTextW(measureStringBuf, measureStringBufLen, textFormat.get(), textRect, measureNumbersBrush.get(), D2D1_DRAW_TEXT_OPTIONS_NO_SNAP);

        rt->EndDraw();

        rt->Flush();
        InvalidateRect(overlayWindow, nullptr, true);
        run_message_loop(true);
    }

    measureToolState.Access([](MeasureToolState::State& state) {
        state.shouldExit = true;
    });
}

HWND DrawOverlayUIThread(MeasureToolState& measureToolState, HMONITOR monitor)
{
    shouldStop = false;

    wil::shared_event windowCreatedEvent(wil::EventOptions::ManualReset);

    HWND window = {};
    std::thread([&measureToolState, monitor, &window, &windowCreatedEvent] {
        CreateOverlayWindowClass();
        window = CreateOverlayUIWindow(monitor);
        windowCreatedEvent.SetEvent();
        DrawOverlayUILoop(measureToolState, window);
    }).detach();

    windowCreatedEvent.wait();

    return window;
}
