#include "pch.h"
#include "AltDrag.h"
#include "framework.h"
#include <windows.h>
#include <windowsx.h>
#include <stdlib.h>
#include "Settings.h"
#include <string.h>
#include <tchar.h>
#include <functional>
#include <mutex>
#include <shared_mutex>

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

struct AltDrag : public winrt::implements<AltDrag, IAltDrag, IAltDragCallback>
{
public:
    AltDrag(HINSTANCE hinstance, const winrt::com_ptr<IAltDragSettings>& settings, std::function<void()> disableModuleCallback) noexcept :
        m_hinstance(hinstance),
        m_settings(settings)
    {
        m_settings->SetCallback(this);

        this->disableModuleCallback = std::move(disableModuleCallback);
    }

    // IAltDrag
    IFACEMETHODIMP_(void)
    Run() noexcept;
    IFACEMETHODIMP_(void)
    Destroy() noexcept;

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen) noexcept
    {
        std::unique_lock writeLock(m_lock);
        //if (m_settings->GetSettings()->spanZonesAcrossMonitors)
       //{
        //    monitor = NULL;
        //}
        //m_windowMoveHandler.MoveSizeStart(window, monitor, ptScreen, m_workAreaHandler.GetWorkAreasByDesktopId(m_currentDesktopId));
    }

    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen) noexcept
    {
        std::unique_lock writeLock(m_lock);
       // if (m_settings->GetSettings()->spanZonesAcrossMonitors)
        //{
         //   monitor = NULL;
       // }
        //m_windowMoveHandler.MoveSizeUpdate(monitor, ptScreen, m_workAreaHandler.GetWorkAreasByDesktopId(m_currentDesktopId));
    }

    void MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
    {
        std::unique_lock writeLock(m_lock);
        //m_windowMoveHandler.MoveSizeEnd(window, ptScreen, m_workAreaHandler.GetWorkAreasByDesktopId(m_currentDesktopId));
    }

    IFACEMETHODIMP_(bool)
    OnKeyEvent(LowlevelKeyboardEvent* info) noexcept;
    IFACEMETHODIMP_(bool)
    OnMouseEvent(LowlevelMouseEvent* info) noexcept;
    IFACEMETHODIMP_(void)
    SettingsChanged() noexcept;

private:
    struct require_read_lock
    {
        template<typename T>
        require_read_lock(const std::shared_lock<T>& lock)
        {
            lock;
        }

        template<typename T>
        require_read_lock(const std::unique_lock<T>& lock)
        {
            lock;
        }
    };

    struct require_write_lock
    {
        template<typename T>
        require_write_lock(const std::unique_lock<T>& lock)
        {
            lock;
        }
    };

    const HINSTANCE m_hinstance{};

    mutable std::shared_mutex m_lock;

    winrt::com_ptr<IAltDragSettings> m_settings{};

    // If non-recoverable error occurs, trigger disabling of entire FancyZones.
    static std::function<void()> disableModuleCallback;

};

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

winrt::com_ptr<IAltDrag> MakeAltDrag(HINSTANCE hinstance,
                                     const winrt::com_ptr<IAltDragSettings>& settings,
                                     std::function<void()> disableCallback) noexcept
{
    if (!settings)
    {
        return nullptr;
    }

    return winrt::make_self<AltDrag>(hinstance, settings, disableCallback);
}


std::function<void()> AltDrag::disableModuleCallback = {};



IFACEMETHODIMP_(void)
AltDrag::Run() noexcept
{
    std::unique_lock writeLock(m_lock);

    WNDCLASSEX wcex;

    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.style = WS_OVERLAPPED;
    wcex.lpfnWndProc = WndProc;
    wcex.cbClsExtra = 0;
    wcex.cbWndExtra = 0;
    wcex.hInstance = m_hinstance;
    wcex.hIcon = NULL;
    wcex.hCursor = LoadCursor(NULL, IDC_HAND);
    wcex.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wcex.lpszMenuName = NULL;
    wcex.lpszClassName = szWindowClass;
    wcex.hIconSm = NULL;

    RegisterClassEx(&wcex);

    // Store instance handle in our global variable
    hInst = m_hinstance;

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
        m_hinstance,
        NULL);

    if (!hWnd)
    {
        MessageBox(NULL,
                   L"Call to CreateWindow failed!",
                   L"Windows Desktop Guided Tour",
                   NULL);

        return;
    }
    globalhwnd = hWnd;

    // Main message loop:
    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    return;
}

IFACEMETHODIMP_(bool)
AltDrag::OnKeyEvent(LowlevelKeyboardEvent* info) noexcept
{
    // Return true to swallow the keyboard event
    //bool const alt = GetAsyncKeyState(VK_LMENU) & 0x8000;
    switch (info->wParam)
    {
    case WM_KEYDOWN:
    case WM_SYSKEYDOWN:
    {
        if (info->lParam->vkCode == VK_LMENU)
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
        break;
    }

    case WM_KEYUP:
    case WM_SYSKEYUP:
    {
    
        if (info->lParam->vkCode == VK_LMENU)
        {
            ShowWindow(globalhwnd, 0);
            altpressed = false;
            return 1;
        }
        break;
    }
    
    default:
        break;    
    }


    return false;
}

IFACEMETHODIMP_(bool)
AltDrag::OnMouseEvent(LowlevelMouseEvent* info) noexcept
{
    switch (info->wParam)
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
            POINT pt = info->lParam->pt;
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
    return false;
}


IFACEMETHODIMP_(void)
AltDrag::SettingsChanged() noexcept
{
    return;
}


// IFancyZones
IFACEMETHODIMP_(void)
AltDrag::Destroy() noexcept
{
    //std::unique_lock writeLock(m_lock);
    //m_workAreaHandler.Clear();
    //BufferedPaintUnInit();
    if (globalhwnd)
    {
        DestroyWindow(globalhwnd);
        globalhwnd = nullptr;
    }
    //if (m_terminateVirtualDesktopTrackerEvent)
    //{
      //  SetEvent(m_terminateVirtualDesktopTrackerEvent.get());
    //}
}
