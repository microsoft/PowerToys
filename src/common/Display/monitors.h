#pragma once
#include <Windows.h>

#include <compare>
#include <optional>
#include <vector>

// TODO: merge with FZ::Rect
struct Box
{
    RECT rect;

    explicit Box(RECT rect = {}) :
        rect(rect) {}
    Box(const Box&) = default;
    Box& operator=(const Box&) = default;

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
    inline bool inside(const POINT point) const { return PtInRect(&rect, point); }

    inline friend auto operator<=>(const Box& lhs, const Box& rhs)
    {
        auto lhs_tuple = std::make_tuple(lhs.rect.left, lhs.rect.right, lhs.rect.top, lhs.rect.bottom);
        auto rhs_tuple = std::make_tuple(rhs.rect.left, rhs.rect.right, rhs.rect.top, rhs.rect.bottom);
        return lhs_tuple <=> rhs_tuple;
    }
};

class MonitorInfo
{
    HMONITOR handle;
    MONITORINFOEX info = {};

public:
    explicit MonitorInfo(HMONITOR h);
    inline HMONITOR GetHandle() const
    {
        return handle;
    }
    Box GetScreenSize(const bool includeNonWorkingArea) const;
    bool IsPrimary() const;

    // Returns monitor rects ordered from left to right
    static std::vector<MonitorInfo> GetMonitors(bool includeNonWorkingArea);
    static MonitorInfo GetPrimaryMonitor();
};
