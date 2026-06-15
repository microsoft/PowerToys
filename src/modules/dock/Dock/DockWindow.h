#pragma once

#include <vector>
#include <string>
#include <shellapi.h>

struct AppItem
{
    HWND hwnd = nullptr;
    HICON icon = nullptr;
    std::wstring title;
    RECT rect{};
    bool active = false;
};

class DockWindow
{
public:
    DockWindow();
    ~DockWindow();

    bool Create(HINSTANCE hInst);
    int Run();
    void Stop();

private:
    static constexpr int ICON_SIZE = 48;
    static constexpr int ICON_PADDING = 8;
    static constexpr int WINDOW_HEIGHT = 72;
    static constexpr UINT AUTO_HIDE_TIMER_ID = 100;
    static constexpr UINT REFRESH_TIMER_ID = 101;
    static constexpr int AUTO_HIDE_MS = 300;
    static constexpr int SHOW_THRESHOLD = 4;

    static LRESULT CALLBACK StaticWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    LRESULT WndProc(UINT msg, WPARAM wParam, LPARAM lParam);

    void OnShellHook(WPARAM wParam, LPARAM lParam);
    void RegisterShellHook();
    void UnregisterShellHook();
    void EnumerateWindows();
    void AddApp(HWND hwnd);
    void RemoveApp(HWND hwnd);
    void RefreshAppActive(HWND hwnd);
    void ClearApps();
    void RebuildLayout();

    void PositionDock();
    void CheckAutoHide();
    bool IsAppWindow(HWND hwnd) const;
    HICON ExtractAppIcon(HWND hwnd) const;
    int HitTest(POINT pt) const;

    HWND m_hwnd = nullptr;
    HINSTANCE m_hInst = nullptr;
    HICON m_defaultIcon = nullptr;
    HANDLE m_hStopEvent = nullptr;
    std::vector<AppItem> m_apps;
    UINT m_shellHookMsg = 0;
    bool m_visible = true;
    bool m_exitRequested = false;
    int m_dockWidth = 100;
    DWORD m_startTime = 0;
};
