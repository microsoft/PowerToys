#include "pch.h"
#include "framework.h"
#include <windows.h>
#include <windowsx.h>
#include <stdlib.h>
#include <string.h>
#include <tchar.h>

// Global variables

// The main window class name.
static TCHAR szWindowClass[] = L"AltDrag";

HWND globalhwnd;
HWND yummyhwnd;

HHOOK llkbdhook;
HHOOK llmshook;

int oldmsx;
int oldmsy;

bool altpressed = false;
bool lmbdown = false;

HINSTANCE hInst;

// Forward declarations of functions included in this code module:
LRESULT CALLBACK WndProc(HWND, UINT, WPARAM, LPARAM);

HWND GetRealParent(HWND hwnd)
{
    return GetAncestor(hwnd, GA_ROOT);
}

LRESULT CALLBACK keyboard_hook(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        switch (wParam)
        {
        case WM_KEYDOWN:
        case WM_SYSKEYDOWN:
        {
            PKBDLLHOOKSTRUCT p = (PKBDLLHOOKSTRUCT)lParam;
            if (p->vkCode == VK_LMENU)
            {
                if (altpressed == false)
                {
                    POINT cursorpos;
                    GetCursorPos(&cursorpos);
                    oldmsx = cursorpos.x;
                    oldmsy = cursorpos.y;
                    yummyhwnd = WindowFromPoint(cursorpos);
                    yummyhwnd = GetRealParent(yummyhwnd);
                    SetWindowPos(globalhwnd, 0, cursorpos.x - 100, cursorpos.y - 100, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                    ShowWindow(globalhwnd, SW_SHOWNA);
                    altpressed = true;
                    return 1;
                }
                else
                {
                    return 1;
                }
            }
        }
        case WM_KEYUP:
        case WM_SYSKEYUP:
        {
            PKBDLLHOOKSTRUCT p = (PKBDLLHOOKSTRUCT)lParam;
            if (p->vkCode == VK_LMENU)
            {
                ShowWindow(globalhwnd, 0);
                altpressed = false;
                return 1;
            }
        }
        default:
            break;
        }
    }

    return CallNextHookEx(NULL, nCode, wParam, lParam);
}

LRESULT CALLBACK mouse_hook(int nCode, WPARAM wParam, LPARAM lParam)
{
    switch (wParam)
    {
    case WM_LBUTTONDOWN:
    {
        if (altpressed)
        {
            lmbdown = true;
            POINT cursorpos;
            GetCursorPos(&cursorpos);
            oldmsx = cursorpos.x;
            oldmsy = cursorpos.y;
            ShowWindow(globalhwnd, SW_HIDE);
            yummyhwnd = WindowFromPoint(cursorpos);
            yummyhwnd = GetRealParent(yummyhwnd);
            ShowWindow(globalhwnd, SW_SHOWNA);
            return 1;
        }
        break;
    }
    case WM_LBUTTONUP:
        lmbdown = false;
        break;
    case WM_MOUSEMOVE:
    {
        if (altpressed)
        {
            PMSLLHOOKSTRUCT p = (PMSLLHOOKSTRUCT)lParam;
            POINT pt = p->pt;
            int xpos = pt.x;
            int deltax = xpos - oldmsx;
            oldmsx = xpos;
            int ypos = pt.y;
            int deltay = ypos - oldmsy;
            oldmsy = ypos;
            SetWindowPos(globalhwnd, 0, xpos - 100, ypos - 100, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

            if (lmbdown)
            {
                WINDOWPLACEMENT place;
                GetWindowPlacement(yummyhwnd, &place);
                RECT placerect = place.rcNormalPosition;
                placerect.left += deltax;
                placerect.right += deltax;
                placerect.top += deltay;
                placerect.bottom += deltay;
                place.rcNormalPosition = placerect;
                SetWindowPlacement(yummyhwnd, &place);
            }
        }
        break;
    }
    default:
        break;
    }

    return CallNextHookEx(NULL, nCode, wParam, lParam);
}

int CALLBACK WinMain(
    _In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPSTR lpCmdLine,
    _In_ int nCmdShow)
{
    WNDCLASSEX wcex;

    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.style = WS_OVERLAPPED;
    wcex.lpfnWndProc = WndProc;
    wcex.cbClsExtra = 0;
    wcex.cbWndExtra = 0;
    wcex.hInstance = hInstance;
    wcex.hIcon = NULL;
    wcex.hCursor = LoadCursor(NULL, IDC_HAND);
    wcex.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wcex.lpszMenuName = NULL;
    wcex.lpszClassName = szWindowClass;
    wcex.hIconSm = NULL;

    RegisterClassEx(&wcex);

    // Store instance handle in our global variable
    hInst = hInstance;

    HWND hWnd = CreateWindowEx(
        WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_LAYERED,
        szWindowClass,
        NULL,
        WS_POPUP,
        0,
        0,
        200,
        200,
        NULL,
        NULL,
        hInstance,
        NULL);

    if (!hWnd)
    {
        MessageBox(NULL,
                   L"Call to CreateWindow failed!",
                   L"Windows Desktop Guided Tour",
                   NULL);

        return 1;
    }

    llkbdhook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboard_hook, hInstance, 0);
    llmshook = SetWindowsHookEx(WH_MOUSE_LL, mouse_hook, hInstance, 0);
    globalhwnd = hWnd;

    // Main message loop:
    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    UnhookWindowsHookEx(llkbdhook);
    UnhookWindowsHookEx(llmshook);

    return (int)msg.wParam;
}

LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
        break;
    }

    return 0;
}
