// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#include "SelectionOverlay.h"
#include "Geometry.h"
#include <windowsx.h>
#include <algorithm>
#include <stdexcept>
#include <string>

namespace RegionMirror
{
    namespace
    {
        constexpr wchar_t SelectionClass[] = L"PowerToys.RegionMirror.Selection";
        constexpr wchar_t BorderClass[] = L"PowerToys.RegionMirror.Border";

        struct OverlayState
        {
            HWND window{};
            POINT start{};
            POINT current{};
            RECT bounds{};
            HMONITOR monitor{};
            bool dragging{};
            bool done{};
            std::optional<Selection> selection;
        };

        POINT ScreenPoint(HWND window, LPARAM position)
        {
            POINT point{ GET_X_LPARAM(position), GET_Y_LPARAM(position) };
            ClientToScreen(window, &point);
            return point;
        }

        void End(OverlayState& state)
        {
            state.done = true;
            state.dragging = false;
            if (GetCapture() == state.window)
            {
                ReleaseCapture();
            }
        }

        LRESULT CALLBACK SelectionProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
        {
            auto state = reinterpret_cast<OverlayState*>(GetWindowLongPtrW(window, GWLP_USERDATA));
            if (message == WM_NCCREATE)
            {
                state = static_cast<OverlayState*>(reinterpret_cast<CREATESTRUCTW*>(lParam)->lpCreateParams);
                state->window = window;
                SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
            }
            if (!state)
            {
                return DefWindowProcW(window, message, wParam, lParam);
            }
            switch (message)
            {
            case WM_LBUTTONDOWN:
            {
                state->start = ScreenPoint(window, lParam);
                state->current = state->start;
                state->monitor = MonitorFromPoint(state->start, MONITOR_DEFAULTTONULL);
                MONITORINFO info{ sizeof(info) };
                if (!state->monitor || !GetMonitorInfoW(state->monitor, &info))
                {
                    End(*state);
                    return 0;
                }
                state->bounds = info.rcMonitor;
                state->dragging = true;
                SetCapture(window);
                return 0;
            }
            case WM_MOUSEMOVE:
                if (state->dragging)
                {
                    const auto point = ScreenPoint(window, lParam);
                    state->current.x = std::clamp(point.x, state->bounds.left, state->bounds.right);
                    state->current.y = std::clamp(point.y, state->bounds.top, state->bounds.bottom);
                    InvalidateRect(window, nullptr, FALSE);
                }
                return 0;
            case WM_LBUTTONUP:
                if (state->dragging)
                {
                    const auto point = ScreenPoint(window, lParam);
                    state->current.x = std::clamp(point.x, state->bounds.left, state->bounds.right);
                    state->current.y = std::clamp(point.y, state->bounds.top, state->bounds.bottom);
                    const auto region = NormalizeSelection(state->start, state->current);
                    if (IsContained(region, state->bounds))
                    {
                        state->selection = Selection{ region, state->monitor };
                    }
                    End(*state);
                }
                return 0;
            case WM_KEYDOWN:
                if (wParam == VK_ESCAPE)
                {
                    End(*state);
                }
                return 0;
            case WM_RBUTTONDOWN:
            case WM_CLOSE:
                End(*state);
                return 0;
            case WM_ACTIVATE:
                if (LOWORD(wParam) == WA_INACTIVE)
                {
                    End(*state);
                }
                return 0;
            case WM_CAPTURECHANGED:
                if (state->dragging)
                {
                    End(*state);
                }
                return 0;
            case WM_ERASEBKGND:
                return 1;
            case WM_PAINT:
            {
                PAINTSTRUCT paint{};
                const auto dc = BeginPaint(window, &paint);
                RECT client{};
                GetClientRect(window, &client);
                FillRect(dc, &client, static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));
                SetTextColor(dc, RGB(255, 255, 255));
                SetBkMode(dc, TRANSPARENT);
                const std::wstring hint = L"Drag to select a region on one screen.  Esc to cancel.";
                POINT hintAt{};
                GetCursorPos(&hintAt);
                ScreenToClient(window, &hintAt);
                TextOutW(dc, hintAt.x + 16, hintAt.y + 24, hint.c_str(), static_cast<int>(hint.size()));
                if (state->dragging)
                {
                    RECT rect = NormalizeSelection(state->start, state->current);
                    MapWindowPoints(nullptr, window, reinterpret_cast<POINT*>(&rect), 2);
                    const auto pen = CreatePen(PS_SOLID, 3, RGB(90, 255, 120));
                    const auto oldPen = SelectObject(dc, pen);
                    const auto oldBrush = SelectObject(dc, GetStockObject(HOLLOW_BRUSH));
                    Rectangle(dc, rect.left, rect.top, rect.right, rect.bottom);
                    SelectObject(dc, oldBrush);
                    SelectObject(dc, oldPen);
                    DeleteObject(pen);
                }
                EndPaint(window, &paint);
                return 0;
            }
            default:
                return DefWindowProcW(window, message, wParam, lParam);
            }
        }

        LRESULT CALLBACK BorderProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
        {
            if (message == WM_NCHITTEST)
            {
                return HTTRANSPARENT;
            }
            if (message == WM_PAINT)
            {
                PAINTSTRUCT paint{};
                const auto dc = BeginPaint(window, &paint);
                RECT client{};
                GetClientRect(window, &client);
                const auto brush = CreateSolidBrush(RGB(30, 220, 90));
                FillRect(dc, &client, brush);
                DeleteObject(brush);
                EndPaint(window, &paint);
                return 0;
            }
            return DefWindowProcW(window, message, wParam, lParam);
        }

        void RegisterOverlayClass(const wchar_t* name, WNDPROC procedure)
        {
            WNDCLASSW windowClass{};
            windowClass.hInstance = GetModuleHandleW(nullptr);
            windowClass.lpfnWndProc = procedure;
            windowClass.lpszClassName = name;
            windowClass.hCursor = LoadCursorW(nullptr, IDC_CROSS);
            if (!RegisterClassW(&windowClass) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
            {
                throw std::runtime_error("Unable to register the selection window.");
            }
        }
    }

    std::optional<Selection> SelectRegion(HWND owner)
    {
        RegisterOverlayClass(SelectionClass, SelectionProc);
        OverlayState state;
        const int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        const int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        const int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        const int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        const auto window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED, SelectionClass, L"Select a region", WS_POPUP, x, y, width, height, owner, nullptr, GetModuleHandleW(nullptr), &state);
        if (!window)
        {
            throw std::runtime_error("Unable to create the selection window.");
        }
        SetLayeredWindowAttributes(window, 0, 115, LWA_ALPHA);
        ShowWindow(window, SW_SHOW);
        SetForegroundWindow(window);
        SetFocus(window);
        MSG message{};
        while (!state.done)
        {
            const auto result = GetMessageW(&message, nullptr, 0, 0);
            if (result <= 0)
            {
                if (result == 0)
                {
                    PostQuitMessage(static_cast<int>(message.wParam));
                }
                break;
            }
            if (message.hwnd == owner && message.message == WM_HOTKEY)
            {
                End(state);
            }
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        End(state);
        DestroyWindow(window);
        return state.selection;
    }

    HWND CreateRegionBorder(const RECT& region)
    {
        RegisterOverlayClass(BorderClass, BorderProc);
        const auto window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
                                            BorderClass,
                                            L"Region Mirror selection",
                                            WS_POPUP,
                                            region.left,
                                            region.top,
                                            region.right - region.left,
                                            region.bottom - region.top,
                                            nullptr,
                                            nullptr,
                                            GetModuleHandleW(nullptr),
                                            nullptr);
        if (!window)
        {
            throw std::runtime_error("Unable to create the region indicator.");
        }
        const int width = region.right - region.left;
        const int height = region.bottom - region.top;
        auto outline = CreateRectRgn(0, 0, width, height);
        auto inside = CreateRectRgn(2, 2, std::max(2, width - 2), std::max(2, height - 2));
        CombineRgn(outline, outline, inside, RGN_DIFF);
        DeleteObject(inside);
        if (!SetWindowRgn(window, outline, TRUE))
        {
            DeleteObject(outline);
        }
        // Only the local indicator is excluded; the mirror output must remain capturable.
        SetWindowDisplayAffinity(window, WDA_EXCLUDEFROMCAPTURE);
        ShowWindow(window, SW_SHOWNOACTIVATE);
        return window;
    }
}
