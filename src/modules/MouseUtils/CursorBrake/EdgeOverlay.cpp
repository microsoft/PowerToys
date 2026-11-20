// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "EdgeOverlay.h"

#include <common/logger/logger.h>

#include <algorithm>
#include <iterator>
#include <memory>

namespace
{
    const wchar_t OVERLAY_WINDOW_CLASS[] = L"CursorBrakeEdgeOverlay";

    // Painted where the window is see-through. Labels sit on opaque pills, so no text is
    // smoothed against it.
    constexpr COLORREF KEY_COLOR = RGB(255, 0, 255);
    constexpr BYTE OVERLAY_ALPHA = 235;

    constexpr UINT_PTR TIMER_CLOSE = 1;
    constexpr UINT_PTR TIMER_POLL = 2;
    constexpr UINT POLL_INTERVAL_MS = 50;

    // Sizes in pixels at 100% scaling.
    constexpr int WALL_DASH = 10;
    constexpr int WALL_GAP = 6;
    constexpr int LABEL_MARGIN = 10;
    constexpr int LABEL_PADDING = 8;
    constexpr int LABEL_FONT = 15;
    constexpr int NUMBER_FONT = 64;

    constexpr COLORREF PILL_FILL = RGB(249, 249, 249);
    constexpr COLORREF PILL_BORDER = RGB(160, 160, 160);
    constexpr COLORREF TEXT_COLOR = RGB(26, 26, 26);
    constexpr COLORREF DEFAULT_STROKE = RGB(138, 138, 138);
    constexpr COLORREF WALL_DARK = RGB(32, 32, 32);
    constexpr COLORREF WALL_LIGHT = RGB(240, 240, 240);

    // Segoe MDL2 Assets carets, present on every supported Windows: right, left, down, up.
    constexpr wchar_t CARET_RIGHT = L'';
    constexpr wchar_t CARET_LEFT = L'';
    constexpr wchar_t CARET_DOWN = L'';
    constexpr wchar_t CARET_UP = L'';

    // The keys and buttons that close the overlay, and which of them were down at the last poll,
    // so only a new press closes it.
    constexpr int CLOSE_KEYS[] = { VK_ESCAPE, VK_LBUTTON, VK_RBUTTON, VK_MBUTTON };

    struct WindowData
    {
        const cursorbrake::OverlayContent* content = nullptr;
        size_t monitor = 0;
    };

    struct PollState
    {
        bool down[std::size(CLOSE_KEYS)] = {};
    };

    HFONT MakeFont(int height, int weight, const wchar_t* face)
    {
        return CreateFontW(-height, 0, 0, 0, weight, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, face);
    }

    void Fill(HDC dc, const RECT& rect, COLORREF color)
    {
        HBRUSH brush = CreateSolidBrush(color);
        FillRect(dc, &rect, brush);
        DeleteObject(brush);
    }

    // A strip in the legend's pattern: Custom solid in the accent color, a wall in dark dashes
    // with light gaps, Off as a thin dashed line.
    void DrawStrip(HDC dc, const RECT& strip, bool sideBySide, cursorbrake::EdgeMode mode, unsigned int dpi)
    {
        const long length = sideBySide ? strip.bottom - strip.top : strip.right - strip.left;
        const auto segment = [&](long start, long end, long inset) {
            RECT part = strip;
            if (sideBySide)
            {
                part.top = strip.top + start;
                part.bottom = strip.top + (std::min)(end, length);
                part.left += inset;
                part.right -= inset;
            }
            else
            {
                part.left = strip.left + start;
                part.right = strip.left + (std::min)(end, length);
                part.top += inset;
                part.bottom -= inset;
            }
            return part;
        };

        const long dash = cursorbrake::Scaled(WALL_DASH, dpi);
        const long gap = cursorbrake::Scaled(WALL_GAP, dpi);
        switch (mode)
        {
        case cursorbrake::EdgeMode::Wall:
            Fill(dc, strip, WALL_LIGHT);
            for (long at = 0; at < length; at += dash + gap)
            {
                Fill(dc, segment(at, at + dash, 0), WALL_DARK);
            }
            break;
        case cursorbrake::EdgeMode::Off:
        {
            const long inset = cursorbrake::Scaled(cursorbrake::EdgeOverlay::STRIP_THICKNESS, dpi) / 3;
            for (long at = 0; at < length; at += dash + gap)
            {
                Fill(dc, segment(at, at + dash, inset), DEFAULT_STROKE);
            }
            break;
        }
        case cursorbrake::EdgeMode::Custom:
        default:
            Fill(dc, strip, GetSysColor(COLOR_HIGHLIGHT));
            break;
        }
    }

    // A label on an opaque pill centered at (x, y). `caret` is drawn after the text when set.
    void DrawPill(HDC dc, const std::wstring& text, wchar_t caret, long x, long y, HFONT font, HFONT caretFont, unsigned int dpi)
    {
        SIZE textSize{};
        HGDIOBJ previous = SelectObject(dc, font);
        GetTextExtentPoint32W(dc, text.c_str(), static_cast<int>(text.size()), &textSize);
        SIZE caretSize{};
        if (caret)
        {
            SelectObject(dc, caretFont);
            GetTextExtentPoint32W(dc, &caret, 1, &caretSize);
        }

        const long padding = cursorbrake::Scaled(LABEL_PADDING, dpi);
        const long spacing = caret ? padding / 2 : 0;
        const long width = textSize.cx + spacing + caretSize.cx + 2 * padding;
        const long height = (std::max)(textSize.cy, caretSize.cy) + padding;
        const RECT pill{ x - width / 2, y - height / 2, x - width / 2 + width, y - height / 2 + height };

        HBRUSH brush = CreateSolidBrush(PILL_FILL);
        HPEN pen = CreatePen(PS_SOLID, 1, PILL_BORDER);
        HGDIOBJ previousBrush = SelectObject(dc, brush);
        HGDIOBJ previousPen = SelectObject(dc, pen);
        RoundRect(dc, pill.left, pill.top, pill.right, pill.bottom, height, height);
        SelectObject(dc, previousBrush);
        SelectObject(dc, previousPen);
        DeleteObject(brush);
        DeleteObject(pen);

        SetBkMode(dc, TRANSPARENT);
        SetTextColor(dc, TEXT_COLOR);
        SelectObject(dc, font);
        TextOutW(dc, pill.left + padding, y - textSize.cy / 2, text.c_str(), static_cast<int>(text.size()));
        if (caret)
        {
            SelectObject(dc, caretFont);
            TextOutW(dc, pill.left + padding + textSize.cx + spacing, y - caretSize.cy / 2, &caret, 1);
        }
        SelectObject(dc, previous);
    }

    void Paint(HWND hwnd, const WindowData& data)
    {
        PAINTSTRUCT paint{};
        HDC dc = BeginPaint(hwnd, &paint);
        RECT client{};
        GetClientRect(hwnd, &client);
        Fill(dc, client, KEY_COLOR);

        const cursorbrake::OverlayContent& content = *data.content;
        const cursorbrake::OverlayMonitor& monitor = content.monitors[data.monitor];
        const unsigned int dpi = monitor.dpi;
        HFONT labelFont = MakeFont(cursorbrake::Scaled(LABEL_FONT, dpi), FW_SEMIBOLD, L"Segoe UI");
        HFONT caretFont = MakeFont(cursorbrake::Scaled(LABEL_FONT, dpi), FW_NORMAL, L"Segoe MDL2 Assets");
        HFONT numberFont = MakeFont(cursorbrake::Scaled(NUMBER_FONT, dpi), FW_BOLD, L"Segoe UI");

        const long margin = cursorbrake::Scaled(LABEL_MARGIN, dpi);
        for (const cursorbrake::OverlayDirection& direction : content.directions)
        {
            if (direction.from != data.monitor)
            {
                continue;
            }
            const RECT strip{ direction.strip.left - monitor.rect.left, direction.strip.top - monitor.rect.top, direction.strip.right - monitor.rect.left, direction.strip.bottom - monitor.rect.top };
            DrawStrip(dc, strip, direction.sideBySide, direction.rule.mode, dpi);

            std::wstring label;
            wchar_t caret = 0;
            switch (direction.rule.mode)
            {
            case cursorbrake::EdgeMode::Wall:
                label = content.wallLabel;
                break;
            case cursorbrake::EdgeMode::Off:
                label = content.offLabel;
                break;
            case cursorbrake::EdgeMode::Custom:
            default:
                label = cursorbrake::ReplacePlaceholder(content.pixelsFormat, std::to_wstring(direction.rule.pushDistance));
                caret = direction.sideBySide ? (direction.toward > 0 ? CARET_RIGHT : CARET_LEFT) : (direction.toward > 0 ? CARET_DOWN : CARET_UP);
                break;
            }

            // The label sits inside the display, beside its strip, half a label's size clear of it.
            SIZE size{};
            HGDIOBJ previous = SelectObject(dc, labelFont);
            GetTextExtentPoint32W(dc, label.c_str(), static_cast<int>(label.size()), &size);
            SelectObject(dc, previous);
            const long halfWidth = size.cx / 2 + cursorbrake::Scaled(2 * LABEL_PADDING, dpi);
            const long halfHeight = size.cy;
            long x = (strip.left + strip.right) / 2;
            long y = (strip.top + strip.bottom) / 2;
            if (direction.sideBySide)
            {
                x = direction.toward > 0 ? strip.left - margin - halfWidth : strip.right + margin + halfWidth;
            }
            else
            {
                y = direction.toward > 0 ? strip.top - margin - halfHeight : strip.bottom + margin + halfHeight;
            }
            DrawPill(dc, label, caret, x, y, labelFont, caretFont, dpi);
        }

        const long centerX = (client.right - client.left) / 2;
        const long centerY = (client.bottom - client.top) / 2;
        DrawPill(dc, std::to_wstring(monitor.number), 0, centerX, centerY, numberFont, caretFont, dpi);
        if (monitor.trapped)
        {
            DrawPill(dc, content.leaveLabel, 0, centerX, centerY + cursorbrake::Scaled(NUMBER_FONT, dpi), labelFont, caretFont, dpi);
        }

        DeleteObject(labelFont);
        DeleteObject(caretFont);
        DeleteObject(numberFont);
        EndPaint(hwnd, &paint);
    }

    LRESULT CALLBACK OverlayWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
        case WM_NCCREATE:
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(reinterpret_cast<CREATESTRUCTW*>(lParam)->lpCreateParams));
            break;
        case WM_PAINT:
            if (const auto* data = reinterpret_cast<const WindowData*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA)))
            {
                Paint(hwnd, *data);
                return 0;
            }
            break;
        case WM_NCHITTEST:
            return HTTRANSPARENT;
        case WM_DISPLAYCHANGE:
            PostQuitMessage(0);
            return 0;
        case WM_TIMER:
            if (wParam == TIMER_CLOSE)
            {
                PostQuitMessage(0);
                return 0;
            }
            break;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}

namespace cursorbrake
{
    int Scaled(int logical, unsigned int dpi)
    {
        return MulDiv(logical, static_cast<int>(dpi), static_cast<int>(BASE_DPI));
    }

    std::wstring ReplacePlaceholder(std::wstring format, const std::wstring& value)
    {
        const size_t at = format.find(L"{0}");
        if (at != std::wstring::npos)
        {
            format.replace(at, 3, value);
        }
        return format;
    }

    EdgeOverlay::~EdgeOverlay()
    {
        Close();
    }

    void EdgeOverlay::Show(OverlayContent content)
    {
        Close();
        m_closeEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!m_closeEvent)
        {
            Logger::error(L"Failed to create the CursorBrake overlay close event, error: {}. Not showing edges.", GetLastError());
            return;
        }
        m_thread = std::thread(Run, std::move(content), m_closeEvent);
    }

    void EdgeOverlay::Close()
    {
        if (m_closeEvent)
        {
            SetEvent(m_closeEvent);
        }
        if (m_thread.joinable())
        {
            m_thread.join();
        }
        if (m_closeEvent)
        {
            CloseHandle(m_closeEvent);
            m_closeEvent = nullptr;
        }
    }

    void EdgeOverlay::Run(OverlayContent content, HANDLE closeEvent)
    {
        // Window positions in physical pixels, matching the layout's rects.
        SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        const HINSTANCE instance = GetModuleHandleW(nullptr);
        WNDCLASSEXW wc = { sizeof(WNDCLASSEXW) };
        wc.lpfnWndProc = OverlayWindowProc;
        wc.hInstance = instance;
        wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        wc.lpszClassName = OVERLAY_WINDOW_CLASS;
        if (!RegisterClassExW(&wc) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
        {
            Logger::error(L"Failed to register the CursorBrake overlay window class, error: {}.", GetLastError());
            return;
        }

        std::vector<std::unique_ptr<WindowData>> data;
        std::vector<HWND> windows;
        for (size_t i = 0; i < content.monitors.size(); ++i)
        {
            const RectL& rect = content.monitors[i].rect;
            data.push_back(std::make_unique<WindowData>(WindowData{ &content, i }));
            HWND window = CreateWindowExW(WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, OVERLAY_WINDOW_CLASS, L"", WS_POPUP, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, nullptr, nullptr, instance, data.back().get());
            if (!window)
            {
                Logger::warn(L"Failed to create a CursorBrake overlay window, error: {}.", GetLastError());
                continue;
            }
            SetLayeredWindowAttributes(window, KEY_COLOR, OVERLAY_ALPHA, LWA_COLORKEY | LWA_ALPHA);
            ShowWindow(window, SW_SHOWNOACTIVATE);
            windows.push_back(window);
        }

        if (!windows.empty())
        {
            SetTimer(windows.front(), TIMER_CLOSE, EdgeOverlay::OVERLAY_DURATION_MS, nullptr);
            SetTimer(windows.front(), TIMER_POLL, POLL_INTERVAL_MS, nullptr);

            PollState poll;
            for (size_t k = 0; k < std::size(CLOSE_KEYS); ++k)
            {
                poll.down[k] = (GetAsyncKeyState(CLOSE_KEYS[k]) & 0x8000) != 0;
            }

            bool running = true;
            while (running)
            {
                const DWORD wait = MsgWaitForMultipleObjects(1, &closeEvent, FALSE, INFINITE, QS_ALLINPUT);
                if (wait != WAIT_OBJECT_0 + 1)
                {
                    break;
                }

                MSG msg;
                while (running && PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
                {
                    if (msg.message == WM_QUIT)
                    {
                        running = false;
                        break;
                    }
                    if (msg.message == WM_TIMER && msg.wParam == TIMER_POLL)
                    {
                        // A key or button pressed since the last poll closes the overlay; one
                        // already down when it opened, such as the click on Show on screens, does
                        // not.
                        for (size_t k = 0; k < std::size(CLOSE_KEYS); ++k)
                        {
                            const bool down = (GetAsyncKeyState(CLOSE_KEYS[k]) & 0x8000) != 0;
                            if (down && !poll.down[k])
                            {
                                running = false;
                            }
                            poll.down[k] = down;
                        }
                        continue;
                    }
                    TranslateMessage(&msg);
                    DispatchMessageW(&msg);
                }
            }
        }

        for (HWND window : windows)
        {
            DestroyWindow(window);
        }
        UnregisterClassW(OVERLAY_WINDOW_CLASS, instance);
    }
}
