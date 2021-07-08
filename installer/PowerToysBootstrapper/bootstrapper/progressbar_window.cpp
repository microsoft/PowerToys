#include "pch.h"

#include <common/utils/window.h>

#include "progressbar_window.h"
#include "Generated Files/resource.h"

const int labelHeight = 18;

const int progressBarHeight = 20;
const int margin = 10;

const int windowWidth = 480;
const int titleBarHeight = 32;
const int windowHeight = margin * 4 + progressBarHeight + labelHeight + titleBarHeight;

int progressBarSteps = 0;

HWND hDialog = nullptr;
HWND hLabel = nullptr;
HWND hProgressBar = nullptr;
HBRUSH hBrush = nullptr;

std::wstring labelText;
std::mutex uiThreadIsRunning;

namespace nonlocalized
{
    const wchar_t windowClass[] = L"PTBProgressBarWnd";
    const wchar_t labelClass[] = L"static";
}

#pragma comment(linker, "\"/manifestdependency:type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

LRESULT CALLBACK WndProc(HWND hWnd, UINT Msg, WPARAM wParam, LPARAM lParam)
{
    switch (Msg)
    {
    case WM_CREATE:
    {
        uiThreadIsRunning.lock();

        hLabel = CreateWindowW(nonlocalized::labelClass,
            labelText.c_str(),
            WS_CHILD | WS_VISIBLE | WS_TABSTOP,
            margin,
            margin,
            windowWidth - (margin * 4),
            labelHeight,
            hWnd,
            (HMENU)(501),
            (HINSTANCE)GetWindowLongPtrW(hWnd, GWLP_HINSTANCE), nullptr);

        hProgressBar = CreateWindowExW(0,
            PROGRESS_CLASS,
            nullptr,
            WS_VISIBLE | WS_CHILD | PBS_SMOOTH,
            margin,
            (margin * 2) + labelHeight,
            windowWidth - (margin * 4),
            progressBarHeight,
            hWnd,
            (HMENU)(IDR_PROGRESS_BAR),
            (HINSTANCE)GetWindowLongPtrW(hWnd, GWLP_HINSTANCE),
            nullptr);

        bool filledOnStart = false;
        if (progressBarSteps == 0)
        {
            progressBarSteps = 1;
            filledOnStart = true;
        }

        SendMessageW(hProgressBar, PBM_SETRANGE, 0, MAKELPARAM(0, progressBarSteps));
        SendMessageW(hProgressBar, PBM_SETSTEP, 1, 0);

        if (filledOnStart)
        {
            SendMessageW(hProgressBar, PBM_STEPIT, 0, 0);
        }

        break;
    }
    case WM_CTLCOLORSTATIC:
    {
      if (lParam == (LPARAM)hLabel)
      {
        if (!hBrush)
        {
            HDC hdcStatic = (HDC)wParam;
            SetTextColor(hdcStatic, RGB(0, 0, 0));
            SetBkColor(hdcStatic, RGB(255, 255, 255));
            hBrush = CreateSolidBrush(RGB(255, 255, 255));
        }

        return (LRESULT)hBrush;
      }
      break;
    }
    case WM_CLOSE:
    {
        DestroyWindow(hWnd);
        PostQuitMessage(0);
        break;
    }
    default:
    {
        return DefWindowProcW(hWnd, Msg, wParam, lParam);
    }
    }
    return 0;
}

void OpenProgressBarDialog(HINSTANCE hInstance, const int nProgressbarSteps, const wchar_t* title, const wchar_t* label)
{
    labelText = label;
    progressBarSteps = nProgressbarSteps;
    std::wstring window_title{ title };
    std::thread{
        [hInstance, window_title = std::move(window_title)] {
            INITCOMMONCONTROLSEX iccex{.dwSize = sizeof(iccex), .dwICC = ICC_NATIVEFNTCTL_CLASS | ICC_PROGRESS_CLASS };
            InitCommonControlsEx(&iccex);

            WNDCLASSEX wc{};
            wc.cbSize = sizeof(WNDCLASSEX);
            wc.lpfnWndProc = WndProc;
            wc.hInstance = hInstance;
            wc.hIcon = LoadIconW(hInstance, MAKEINTRESOURCE(IDR_BIN_ICON));
            wc.hIconSm = LoadIconW(hInstance, MAKEINTRESOURCE(IDR_BIN_ICON));
            wc.lpszClassName = nonlocalized::windowClass;

            if (!RegisterClassExW(&wc))
            {
                spdlog::warn("Couldn't register main_window class for progress bar.");
                return;
            }

            RECT rect{};
            GetClientRect(GetDesktopWindow(), &rect);
            rect.left = rect.right / 2 - windowWidth / 2;
            rect.top = rect.bottom / 4 - windowHeight / 2;
            hDialog = CreateWindowExW(WS_EX_CLIENTEDGE,
                nonlocalized::windowClass,
                window_title.c_str(),
                WS_OVERLAPPED | WS_CAPTION | WS_MINIMIZEBOX,
                rect.left,
                rect.top,
                windowWidth,
                windowHeight,
                nullptr,
                nullptr,
                hInstance,
                nullptr);

            if (!hDialog)
            {
                spdlog::warn("Couldn't create progress bar main_window");
                return;
            }

            ShowWindow(hDialog, SW_SHOW);
            UpdateWindow(hDialog);
            run_message_loop();
            uiThreadIsRunning.unlock();
        }
    }.detach();
}

void UpdateProgressBarDialog(const wchar_t* label)
{
    SetWindowTextW(hLabel, label);
    SendMessageW(hProgressBar, PBM_STEPIT, 0, 0);
}

void CloseProgressBarDialog()
{
    SendMessageW(hDialog, WM_CLOSE, {}, {});
    {
        std::unique_lock waitForUIToExit{ uiThreadIsRunning };
    }

    // Return focus to the current process, since it was lost due to progress bar closing (?)
    INPUT i = {INPUT_MOUSE, {}};
    SendInput(1, &i, sizeof(i));
    SetForegroundWindow(GetActiveWindow());

}