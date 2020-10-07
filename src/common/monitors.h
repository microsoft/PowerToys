#pragma once
#include <Windows.h>
#include <vector>

struct ScreenSize
{
    explicit ScreenSize(RECT rect) :
        rect(rect) {}
    RECT rect;
    int left() const { return rect.left; }
    int right() const { return rect.right; }
    int top() const { return rect.top; }
    int bottom() const { return rect.bottom; }
    int height() const { return rect.bottom - rect.top; };
    int width() const { return rect.right - rect.left; };
    POINT top_left() const { return { rect.left, rect.top }; };
    POINT top_middle() const { return { rect.left + width() / 2, rect.top }; };
    POINT top_right() const { return { rect.right, rect.top }; };
    POINT middle_left() const { return { rect.left, rect.top + height() / 2 }; };
    POINT middle() const { return { rect.left + width() / 2, rect.top + height() / 2 }; };
    POINT middle_right() const { return { rect.right, rect.top + height() / 2 }; };
    POINT bottom_left() const { return { rect.left, rect.bottom }; };
    POINT bottom_middle() const { return { rect.left + width() / 2, rect.bottom }; };
    POINT bottom_right() const { return { rect.right, rect.bottom }; };
};

struct MonitorInfo : ScreenSize
{
    explicit MonitorInfo(HMONITOR monitor, RECT rect) :
        handle(monitor), ScreenSize(rect) {}
    HMONITOR handle;

    // Returns monitor rects ordered from left to right
    static std::vector<MonitorInfo> GetMonitors(bool includeNonWorkingArea);
    static MonitorInfo GetPrimaryMonitor();
    static MonitorInfo GetFromWindow(HWND hwnd);
    static MonitorInfo GetFromPoint(POINT p);
    static MonitorInfo GetFromHandle(HMONITOR monitor);
};

bool operator==(const ScreenSize& lhs, const ScreenSize& rhs);
