// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CursorBrakeCore.h"

#include <algorithm>
#include <array>
#include <cstdint>
#include <optional>
#include <string_view>

// What CursorBrake does while a full-screen app or game is in front, decoupled from Win32 so it
// can be unit tested. The hook reads the foreground window and passes what it found.
namespace cursorbrake
{
    // The choice for full-screen apps, as `fullscreen_behavior` stores it.
    enum class FullScreenBehavior
    {
        // The edges of the full-screen app's monitor brake like any other.
        Same,
        // Every way out of the full-screen app's monitor is a wall only the cross key crosses.
        Wall,
        // Moves out of the full-screen app's monitor go through, walls included.
        NoResistance,
    };

    constexpr int FULLSCREEN_BEHAVIOR_COUNT = 3;

    // Whether `window` reaches every side of `rect`.
    constexpr bool Covers(const RectL& window, const RectL& rect)
    {
        return window.left <= rect.left && window.top <= rect.top && window.right >= rect.right && window.bottom >= rect.bottom;
    }

    // Whether the foreground window is a full-screen app on `monitor`, the rect of the monitor
    // holding it: it covers that whole rect, has no caption, and is not the shell or the desktop.
    // The caption keeps a maximized window under an auto-hidden taskbar out. A window reaching
    // past the monitor, as exclusive mode can, still covers it.
    constexpr bool IsFullScreen(const RectL& window, const RectL& monitor, bool hasCaption, bool isShellOrDesktop)
    {
        return Covers(window, monitor) && monitor.right > monitor.left && monitor.bottom > monitor.top && !hasCaption && !isShellOrDesktop;
    }

    // What the hook does with a move: let it through as a bypass, or judge it with the wall
    // monitor to give the engine.
    struct FullScreenTreatment
    {
        bool pass = false;
        std::optional<size_t> wallMonitor;

        bool operator==(const FullScreenTreatment&) const = default;
    };

    // The treatment of a move starting on `source` while `fullScreenMonitor` holds a full-screen
    // app, either one unknown when absent. No resistance passes only moves starting on the
    // full-screen monitor; the engine applies a wall only to the way out of the monitor it names.
    constexpr FullScreenTreatment TreatFullScreen(FullScreenBehavior behavior, std::optional<size_t> fullScreenMonitor, std::optional<size_t> source)
    {
        if (!fullScreenMonitor)
        {
            return FullScreenTreatment{};
        }
        switch (behavior)
        {
        case FullScreenBehavior::Wall:
            return FullScreenTreatment{ false, fullScreenMonitor };
        case FullScreenBehavior::NoResistance:
            return FullScreenTreatment{ source == fullScreenMonitor, std::nullopt };
        case FullScreenBehavior::Same:
        default:
            return FullScreenTreatment{};
        }
    }

    // The index of the monitor in the layout whose rect is exactly `rect`.
    inline std::optional<size_t> MonitorWithRect(const Layout& layout, const RectL& rect)
    {
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            if (layout.monitors[i].rect == rect)
            {
                return i;
            }
        }
        return std::nullopt;
    }

    // What the hook reads about the foreground window.
    struct ForegroundWindow
    {
        RectL rect;
        // The rect of the monitor holding the window, as MonitorFromWindow names it.
        RectL monitor;
        bool hasCaption = false;
        bool isShellOrDesktop = false;
        bool isToolWindow = false;
    };

    // The executable names of PowerToys' own overlays that put one captionless window over each
    // monitor and take the foreground: Screen Ruler, Text Extractor and the Workspaces editor's
    // snapshot. Text Extractor's and the Workspaces editor's windows are no tool windows.
    inline constexpr std::array<std::wstring_view, 3> POWERTOYS_OVERLAYS = {
        L"POWERTOYS.MEASURETOOLUI.EXE",
        L"POWERTOYS.POWEROCR.EXE",
        L"POWERTOYS.WORKSPACESEDITOR.EXE",
    };

    // Whether `path`, an uppercased process path, names one of POWERTOYS_OVERLAYS by its file name.
    inline bool IsPowerToysOverlay(std::wstring_view path)
    {
        const size_t slash = path.find_last_of(L'\\');
        const std::wstring_view name = slash == std::wstring_view::npos ? path : path.substr(slash + 1);
        return std::find(POWERTOYS_OVERLAYS.begin(), POWERTOYS_OVERLAYS.end(), name) != POWERTOYS_OVERLAYS.end();
    }

    // The monitor of the layout holding a full-screen app, from what the hook read about the
    // foreground window. A window that covers more than one monitor is one app across them, as
    // Remote Desktop on all monitors or a stretched simulator is, and a tool window or one of
    // PowerToys' own overlays is an overlay over one monitor of several. None of these is a
    // full-screen app. `isOverlay` says whether the window belongs to one of PowerToys' own
    // overlays, and runs only for a window that is otherwise full screen, since it opens the
    // window's process.
    template<typename IsOverlay>
    std::optional<size_t> FullScreenMonitorOf(const Layout& layout, const ForegroundWindow& window, IsOverlay&& isOverlay)
    {
        if (window.isToolWindow || !IsFullScreen(window.rect, window.monitor, window.hasCaption, window.isShellOrDesktop))
        {
            return std::nullopt;
        }
        const auto covered = std::count_if(layout.monitors.begin(), layout.monitors.end(), [&](const Monitor& monitor) {
            return Covers(window.rect, monitor.rect);
        });
        if (covered > 1)
        {
            return std::nullopt;
        }
        const std::optional<size_t> monitor = MonitorWithRect(layout, window.monitor);
        if (!monitor || isOverlay())
        {
            return std::nullopt;
        }
        return monitor;
    }

    // The monitor holding a full-screen app, as last worked out. The hook works it out only for a
    // move that proposes a crossing, since only such a move can meet the wall or pass, and keeps
    // it for every other move. A change then reaches the engine on the first crossing after it,
    // never on a move between.
    class FullScreenTracker
    {
    public:
        // `probe` returns the monitor holding a full-screen app now, and runs only for a move that
        // proposes a crossing.
        template<typename Probe>
        std::optional<size_t> Observe(bool proposesCrossing, Probe&& probe)
        {
            if (proposesCrossing)
            {
                m_monitor = probe();
            }
            return m_monitor;
        }

        void Reset()
        {
            m_monitor.reset();
        }

    private:
        std::optional<size_t> m_monitor;
    };

    // How the hook learned which window is in front.
    enum class ForegroundReport
    {
        // A foreground event, which names no window while the foreground switches.
        Event,
        // GetForegroundWindow, at start, on a settings apply, on a session change or on a second
        // look.
        Query,
    };

    // Whether the app in front is one of the excluded apps, kept between foreground changes.
    class ForegroundCache
    {
    public:
        // `judge` returns whether `window` belongs to an excluded app, and runs only for a window
        // while the list has entries. An event naming no window keeps the last answer.
        template<typename Judge>
        void Adopt(uintptr_t window, ForegroundReport report, bool hasExcludedApps, Judge&& judge)
        {
            if (!hasExcludedApps)
            {
                m_excluded = false;
            }
            else if (window != 0)
            {
                m_excluded = judge(window);
            }
            else if (report == ForegroundReport::Query)
            {
                m_excluded = false;
            }
        }

        bool Excluded() const
        {
            return m_excluded;
        }

    private:
        bool m_excluded = false;
    };
}
