#include "pch.h"
#include "DockWindow.h"

#include <common/utils/winapi_error.h>
#include <common/logger/logger.h>

#include "ModuleConstants.h"

DockWindow::DockWindow()
{
    m_defaultIcon = LoadIcon(nullptr, IDI_APPLICATION);
    m_hStopEvent = CreateEventW(nullptr, TRUE, FALSE, NonLocalizable::StopEventName);
    m_startTime = GetTickCount64();
}

DockWindow::~DockWindow()
{
    if (m_defaultIcon)
    {
        DestroyIcon(m_defaultIcon);
    }
    if (m_hStopEvent)
    {
        CloseHandle(m_hStopEvent);
    }
    ClearApps();
}

bool DockWindow::Create(HINSTANCE hInst)
{
    m_hInst = hInst;

    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = StaticWndProc;
    wc.hInstance = hInst;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = CreateSolidBrush(RGB(30, 30, 35));
    wc.lpszClassName = L"PowerToysDockWindow";

    if (!RegisterClassExW(&wc))
    {
        Logger::error(L"DockWindow: Failed to register window class");
        return false;
    }

    m_hwnd = CreateWindowExW(
        WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
        wc.lpszClassName,
        L"PowerToys Dock",
        WS_POPUP,
        0, 0, 100, WINDOW_HEIGHT,
        nullptr, nullptr, hInst, this);

    if (!m_hwnd)
    {
        Logger::error(L"DockWindow: Failed to create window");
        return false;
    }

    return true;
}

int DockWindow::Run()
{
    EnumerateWindows();
    PositionDock();
    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    RegisterShellHook();

    SetTimer(m_hwnd, AUTO_HIDE_TIMER_ID, AUTO_HIDE_MS, nullptr);
    SetTimer(m_hwnd, REFRESH_TIMER_ID, 3000, nullptr);

    MSG msg = {};
    while (!m_exitRequested && GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);

        if (m_hStopEvent && WaitForSingleObject(m_hStopEvent, 0) == WAIT_OBJECT_0)
        {
            m_exitRequested = true;
        }
    }

    KillTimer(m_hwnd, AUTO_HIDE_TIMER_ID);
    KillTimer(m_hwnd, REFRESH_TIMER_ID);
    UnregisterShellHook();

    return 0;
}

void DockWindow::Stop()
{
    m_exitRequested = true;
    if (m_hStopEvent)
    {
        SetEvent(m_hStopEvent);
    }
    if (m_hwnd)
    {
        PostMessage(m_hwnd, WM_CLOSE, 0, 0);
    }
}

LRESULT CALLBACK DockWindow::StaticWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    DockWindow* self = nullptr;
    if (msg == WM_NCCREATE)
    {
        auto cs = reinterpret_cast<CREATESTRUCTW*>(lParam);
        self = static_cast<DockWindow*>(cs->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
    }
    else
    {
        self = reinterpret_cast<DockWindow*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    }

    if (self)
    {
        return self->WndProc(msg, wParam, lParam);
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

LRESULT DockWindow::WndProc(UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_CREATE:
        m_shellHookMsg = RegisterWindowMessageW(L"SHELLHOOK");
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;

    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(m_hwnd, &ps);

        RECT clientRect;
        GetClientRect(m_hwnd, &clientRect);

        HDC hdcMem = CreateCompatibleDC(hdc);
        HBITMAP hBmp = CreateCompatibleBitmap(hdc, m_dockWidth, m_dockHeight);
        HBITMAP hOldBmp = static_cast<HBITMAP>(SelectObject(hdcMem, hBmp));

        HBRUSH hBgBrush = CreateSolidBrush(RGB(30, 30, 35));
        FillRect(hdcMem, &clientRect, hBgBrush);
        DeleteObject(hBgBrush);

        for (size_t i = 0; i < m_apps.size(); ++i)
        {
            auto& app = m_apps[i];

            if (app.active)
            {
                RECT activeBg = app.rect;
                InflateRect(&activeBg, 2, 2);
                HBRUSH hActiveBrush = CreateSolidBrush(RGB(55, 55, 60));
                FillRect(hdcMem, &activeBg, hActiveBrush);
                DeleteObject(hActiveBrush);
            }

            if (app.icon)
            {
                int iconX = app.rect.left + (app.rect.right - app.rect.left - ICON_SIZE) / 2;
                int iconY = app.rect.top + 2;
                DrawIconEx(hdcMem, iconX, iconY, app.icon, ICON_SIZE, ICON_SIZE, 0, nullptr, DI_NORMAL);
            }

            SetBkMode(hdcMem, TRANSPARENT);
            SetTextColor(hdcMem, RGB(220, 220, 220));

            HFONT hFont = CreateFontW(10, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                                       DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                                       CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
            HFONT hOldFont = static_cast<HFONT>(SelectObject(hdcMem, hFont));

            RECT textRect = app.rect;
            textRect.top = textRect.bottom - 16;
            DrawTextW(hdcMem, app.title.c_str(), static_cast<int>(app.title.length()),
                       &textRect, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);

            SelectObject(hdcMem, hOldFont);
            DeleteObject(hFont);
        }

        BitBlt(hdc, 0, 0, m_dockWidth, m_dockHeight, hdcMem, 0, 0, SRCCOPY);

        SelectObject(hdcMem, hOldBmp);
        DeleteObject(hBmp);
        DeleteDC(hdcMem);

        EndPaint(m_hwnd, &ps);
        return 0;
    }

    case WM_TIMER:
    {
        UINT id = static_cast<UINT>(wParam);
        if (id == AUTO_HIDE_TIMER_ID)
        {
            CheckAutoHide();
        }
        else if (id == REFRESH_TIMER_ID)
        {
            bool changed = false;
            size_t oldCount = m_apps.size();
            EnumerateWindows();
            if (m_apps.size() != oldCount)
            {
                changed = true;
            }
            if (changed)
            {
                InvalidateRect(m_hwnd, nullptr, TRUE);
            }
        }
        return 0;
    }

    case WM_LBUTTONDOWN:
    {
        POINT pt = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        int index = HitTest(pt);
        if (index >= 0 && index < static_cast<int>(m_apps.size()))
        {
            auto hwnd = m_apps[index].hwnd;
            if (IsWindow(hwnd))
            {
                if (IsIconic(hwnd))
                {
                    ShowWindow(hwnd, SW_RESTORE);
                }
                SetForegroundWindow(hwnd);
            }
        }
        return 0;
    }

    case WM_DISPLAYCHANGE:
        PositionDock();
        InvalidateRect(m_hwnd, nullptr, TRUE);
        return 0;

    default:
        if (msg == m_shellHookMsg)
        {
            OnShellHook(wParam, lParam);
            return 0;
        }
        break;
    }

    return DefWindowProcW(m_hwnd, msg, wParam, lParam);
}

void DockWindow::OnShellHook(WPARAM wParam, LPARAM lParam)
{
    auto hwnd = reinterpret_cast<HWND>(lParam);
    bool changed = false;

    switch (wParam)
    {
    case HSHELL_WINDOWCREATED:
    case HSHELL_RUDEAPPACTIVATED:
        if (IsAppWindow(hwnd))
        {
            AddApp(hwnd);
            changed = true;
        }
        break;

    case HSHELL_WINDOWDESTROYED:
        RemoveApp(hwnd);
        changed = true;
        break;

    case HSHELL_WINDOWACTIVATED:
        RefreshAppActive(hwnd);
        changed = true;
        break;

    case HSHELL_WINDOWREPLACED:
        RemoveApp(hwnd);
        changed = true;
        break;

    case HSHELL_WINDOWREPLACING:
        if (IsAppWindow(hwnd))
        {
            AddApp(hwnd);
            changed = true;
        }
        break;
    }

    if (changed)
    {
        InvalidateRect(m_hwnd, nullptr, TRUE);
    }
}

void DockWindow::RegisterShellHook()
{
    if (m_hwnd && m_shellHookMsg)
    {
        RegisterShellHookWindow(m_hwnd);
    }
}

void DockWindow::UnregisterShellHook()
{
    if (m_hwnd)
    {
        DeregisterShellHookWindow(m_hwnd);
    }
}

void DockWindow::EnumerateWindows()
{
    struct EnumData
    {
        DockWindow* self;
    };

    EnumData data = { this };

    EnumWindows([](HWND hwnd, LPARAM lParam) -> BOOL {
        auto data = reinterpret_cast<EnumData*>(lParam);
        if (data->self->IsAppWindow(hwnd))
        {
            data->self->AddApp(hwnd);
        }
        return TRUE;
    }, reinterpret_cast<LPARAM>(&data));
}

void DockWindow::AddApp(HWND hwnd)
{
    for (auto& app : m_apps)
    {
        if (app.hwnd == hwnd)
        {
            if (!app.icon)
            {
                app.icon = ExtractAppIcon(hwnd);
            }
            return;
        }
    }

    if (m_apps.size() >= 40)
    {
        return;
    }

    AppItem app;
    app.hwnd = hwnd;
    app.icon = ExtractAppIcon(hwnd);

    wchar_t title[256] = {};
    GetWindowTextW(hwnd, title, 256);
    app.title = title;

    if (app.title.empty())
    {
        if (app.icon && app.icon != m_defaultIcon)
        {
            DestroyIcon(app.icon);
        }
        return;
    }

    app.active = (hwnd == GetForegroundWindow());

    m_apps.push_back(std::move(app));
    RebuildLayout();
}

void DockWindow::RemoveApp(HWND hwnd)
{
    for (auto it = m_apps.begin(); it != m_apps.end(); ++it)
    {
        if (it->hwnd == hwnd)
        {
            if (it->icon && it->icon != m_defaultIcon)
            {
                DestroyIcon(it->icon);
            }
            m_apps.erase(it);
            RebuildLayout();
            return;
        }
    }
}

void DockWindow::RefreshAppActive(HWND hwnd)
{
    for (auto& app : m_apps)
    {
        app.active = (app.hwnd == hwnd);
    }
}

void DockWindow::ClearApps()
{
    for (auto& app : m_apps)
    {
        if (app.icon && app.icon != m_defaultIcon)
        {
            DestroyIcon(app.icon);
        }
    }
    m_apps.clear();
}

void DockWindow::RebuildLayout()
{
    int count = static_cast<int>(m_apps.size());
    int totalWidth = count * (ICON_SIZE + ICON_PADDING) + ICON_PADDING;
    m_dockWidth = (std::max)(totalWidth, 100);

    for (int i = 0; i < count; ++i)
    {
        int x = ICON_PADDING + i * (ICON_SIZE + ICON_PADDING);
        int y = 2;
        SetRect(&m_apps[i].rect, x, y, x + ICON_SIZE + ICON_PADDING / 2, y + ICON_SIZE + 18);
    }

    PositionDock();
}

void DockWindow::PositionDock()
{
    int screenWidth = GetSystemMetrics(SM_CXSCREEN);
    int screenHeight = GetSystemMetrics(SM_CYSCREEN);

    int x = (screenWidth - m_dockWidth) / 2;
    int y = screenHeight - WINDOW_HEIGHT - 4;

    SetWindowPos(m_hwnd, HWND_TOPMOST, x, y, m_dockWidth, WINDOW_HEIGHT, SWP_NOACTIVATE);
}

void DockWindow::CheckAutoHide()
{
    if (GetTickCount64() - m_startTime < 3000)
    {
        return;
    }

    POINT pt;
    GetCursorPos(&pt);

    int screenHeight = GetSystemMetrics(SM_CYSCREEN);

    RECT dockRect;
    GetWindowRect(m_hwnd, &dockRect);

    bool mouseNearBottom = (pt.y >= screenHeight - SHOW_THRESHOLD);
    bool mouseInDock = PtInRect(&dockRect, pt);

    if (mouseInDock || mouseNearBottom)
    {
        if (!m_visible)
        {
            m_visible = true;
            SetWindowPos(m_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                         SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }
    else if (m_visible)
    {
        m_visible = false;
        ShowWindow(m_hwnd, SW_HIDE);
    }
}

bool DockWindow::IsAppWindow(HWND hwnd) const
{
    if (!IsWindowVisible(hwnd))
    {
        return false;
    }

    auto style = GetWindowLongPtrW(hwnd, GWL_STYLE);
    auto exStyle = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);

    if (!(style & WS_POPUP) && !(style & WS_OVERLAPPED))
    {
        return false;
    }

    if (exStyle & WS_EX_TOOLWINDOW)
    {
        return false;
    }

    wchar_t className[64] = {};
    GetClassNameW(hwnd, className, 64);
    if (wcscmp(className, L"Windows.UI.Core.CoreWindow") == 0 ||
        wcscmp(className, L"ApplicationFrameWindow") == 0 ||
        wcscmp(className, L"Progman") == 0 ||
        wcscmp(className, L"WorkerW") == 0)
    {
        return false;
    }

    wchar_t title[256] = {};
    if (GetWindowTextW(hwnd, title, 256) == 0 || title[0] == L'\0')
    {
        return false;
    }

    return true;
}

HICON DockWindow::ExtractAppIcon(HWND hwnd) const
{
    HICON icon = reinterpret_cast<HICON>(SendMessageW(hwnd, WM_GETICON, ICON_BIG, 0));
    if (!icon)
        icon = reinterpret_cast<HICON>(SendMessageW(hwnd, WM_GETICON, ICON_SMALL, 0));
    if (!icon)
        icon = reinterpret_cast<HICON>(GetClassLongPtrW(hwnd, GCLP_HICON));
    if (!icon)
        icon = reinterpret_cast<HICON>(GetClassLongPtrW(hwnd, GCLP_HICONSM));

    if (!icon)
    {
        DWORD pid = 0;
        GetWindowThreadProcessId(hwnd, &pid);
        HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);
        if (hProcess)
        {
            wchar_t path[MAX_PATH] = {};
            DWORD size = MAX_PATH;
            if (QueryFullProcessImageNameW(hProcess, 0, path, &size))
            {
                SHFILEINFOW sfi = {};
                if (SHGetFileInfoW(path, 0, &sfi, sizeof(sfi), SHGFI_ICON | SHGFI_LARGEICON) && sfi.hIcon)
                {
                    icon = sfi.hIcon;
                }
            }
            CloseHandle(hProcess);
        }
    }

    return icon ? icon : m_defaultIcon;
}

int DockWindow::HitTest(POINT pt) const
{
    for (size_t i = 0; i < m_apps.size(); ++i)
    {
        if (PtInRect(&m_apps[i].rect, pt))
        {
            return static_cast<int>(i);
        }
    }
    return -1;
}
