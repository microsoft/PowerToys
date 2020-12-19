#include "pch.h"

#include <common/updating/notifications.h>
#include <common/utils/window.h>

#include "progressbar_window.h"
#include "Generated Files/resource.h"

const int label_height = 20;

const int progress_bar_height = 15;
const int progress_bar_margin = 10;

const int window_width = 450;
const int title_bar_height = 32;
const int window_height = progress_bar_margin * 3 + progress_bar_height + label_height + title_bar_height;

int progressbar_steps = 0;

HWND progress_bar;
HWND main_window;
HWND label;

std::wstring initial_label;
std::mutex ui_thread_is_running;

namespace nonlocalized
{
    const wchar_t window_class[] = L"PTBProgressBarWnd";
    const wchar_t label_class[] = L"static";
}

#pragma comment(linker, "\"/manifestdependency:type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

LRESULT CALLBACK WndProc(HWND hWnd, UINT Msg, WPARAM wParam, LPARAM lParam)
{
    switch (Msg)
    {
    case WM_CREATE:
    {
        ui_thread_is_running.lock();
        label = CreateWindowW(nonlocalized::label_class, initial_label.c_str(), WS_CHILD | WS_VISIBLE | WS_TABSTOP, progress_bar_margin, 0, window_width - progress_bar_margin * 4, label_height, hWnd, (HMENU)(501), (HINSTANCE)GetWindowLongPtrW(hWnd, GWLP_HINSTANCE), nullptr);

        progress_bar = CreateWindowExW(0,
                                       PROGRESS_CLASS,
                                       nullptr,
                                       WS_VISIBLE | WS_CHILD | PBS_SMOOTH,
                                       progress_bar_margin,
                                       progress_bar_margin + label_height,
                                       window_width - progress_bar_margin * 4,
                                       progress_bar_height,
                                       hWnd,
                                       (HMENU)(IDR_PROGRESS_BAR),
                                       (HINSTANCE)GetWindowLongPtrW(hWnd, GWLP_HINSTANCE),
                                       nullptr);

        bool filled_on_start = false;
        if (progressbar_steps == 0)
        {
            progressbar_steps = 1;
            filled_on_start = true;
        }
        SendMessageW(progress_bar, PBM_SETRANGE, 0, MAKELPARAM(0, progressbar_steps));
        SendMessageW(progress_bar, PBM_SETSTEP, 1, 0);
        if (filled_on_start)
        {
            SendMessageW(progress_bar, PBM_STEPIT, 0, 0);
        }

        break;
    }
    case WM_CLOSE:
        DestroyWindow(hWnd);
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProcW(hWnd, Msg, wParam, lParam);
    }
    return 0;
}

void open_progressbar_window(HINSTANCE hInstance, const int n_progressbar_steps, const wchar_t* title, const wchar_t* init_label)
{
    initial_label = init_label;
    progressbar_steps = n_progressbar_steps;
    std::wstring window_title{ title };
    std::thread{
        [hInstance, window_title = std::move(window_title)] {
            INITCOMMONCONTROLSEX iccex{ .dwSize = sizeof(iccex), .dwICC = ICC_NATIVEFNTCTL_CLASS | ICC_PROGRESS_CLASS };
            InitCommonControlsEx(&iccex);

            WNDCLASSEX wc{};
            wc.cbSize = sizeof(WNDCLASSEX);
            wc.lpfnWndProc = WndProc;
            wc.hInstance = hInstance;
            wc.hIcon = LoadIconW(hInstance, MAKEINTRESOURCE(IDR_BIN_ICON));
            wc.hIconSm = LoadIconW(hInstance, MAKEINTRESOURCE(IDR_BIN_ICON));
            wc.lpszClassName = nonlocalized::window_class;
            if (!RegisterClassExW(&wc))
            {
                spdlog::warn("Couldn't register main_window class for progress bar.");
                return;
            }
            RECT rect{};
            GetClientRect(GetDesktopWindow(), &rect);
            rect.left = rect.right / 2 - window_width / 2;
            rect.top = rect.bottom / 4 - window_height / 2;
            main_window = CreateWindowExW(WS_EX_CLIENTEDGE,
                                          nonlocalized::window_class,
                                          window_title.c_str(),
                                          WS_OVERLAPPED | WS_CAPTION | WS_MINIMIZEBOX,
                                          rect.left,
                                          rect.top,
                                          window_width,
                                          window_height,
                                          nullptr,
                                          nullptr,
                                          hInstance,
                                          nullptr);

            if (!main_window)
            {
                spdlog::warn("Couldn't create progress bar main_window");
                return;
            }
            ShowWindow(main_window, SW_SHOW);
            UpdateWindow(main_window);
            run_message_loop();
            ui_thread_is_running.unlock();
        }
    }.detach();
}

void tick_progressbar_window(const wchar_t* new_status)
{
    SetWindowTextW(label, new_status);
    SendMessageW(progress_bar, PBM_STEPIT, 0, 0);
}

void close_progressbar_window()
{
    SendMessageW(main_window, WM_CLOSE, {}, {});
    {
        std::unique_lock wait_for_ui_to_exit{ui_thread_is_running};
    }
    // Return focus to the current process, since it was lost due to progress bar closing (?)
    INPUT i = {INPUT_MOUSE, {}};
    SendInput(1, &i, sizeof(i));
    SetForegroundWindow(GetActiveWindow());

}