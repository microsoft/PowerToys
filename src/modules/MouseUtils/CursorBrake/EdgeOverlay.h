// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>

#include "CursorBrakeCore.h"

#include <atomic>
#include <string>
#include <thread>
#include <vector>

namespace cursorbrake
{
    // `logical` pixels at 100% scaling as pixels at `dpi`, rounded to the nearest.
    int Scaled(int logical, unsigned int dpi);

    // `format` with its first "{0}" replaced by `value`, the form the overlay's resource strings
    // take their key or strength in.
    std::wstring ReplacePlaceholder(std::wstring format, const std::wstring& value);

    // What "Show on screens" draws on one display: its number, and whether it can only be left
    // with the cross key.
    struct OverlayMonitor
    {
        RectL rect;
        unsigned int dpi = BASE_DPI;
        int number = 0;
        bool trapped = false;
    };

    // One direction across an edge, drawn on the display it leaves. `toward` points across the
    // seam: +1 to the right or down, -1 to the left or up.
    struct OverlayDirection
    {
        size_t from = 0;
        RectL strip;
        bool sideBySide = true;
        int toward = 1;
        EdgeRule rule;
    };

    struct OverlayContent
    {
        std::vector<OverlayMonitor> monitors;
        std::vector<OverlayDirection> directions;
        // "Wall: hold Ctrl to cross", "Leave with Ctrl", "Off", and "{0} px" for a strength.
        std::wstring wallLabel;
        std::wstring leaveLabel;
        std::wstring offLabel;
        std::wstring pixelsFormat;
    };

    // "Show on screens": click-through, topmost windows over every display, drawn on a
    // short-lived thread of their own, so the hook thread never paints. They close after
    // OVERLAY_DURATION_MS, on Esc or a mouse click, or on a display change.
    class EdgeOverlay
    {
    public:
        static constexpr UINT OVERLAY_DURATION_MS = 5000;

        // How deep a direction's strip reaches into the display it leaves, in pixels at 100%
        // scaling.
        static constexpr int STRIP_THICKNESS = 12;

        EdgeOverlay() = default;
        ~EdgeOverlay();
        EdgeOverlay(const EdgeOverlay&) = delete;
        EdgeOverlay& operator=(const EdgeOverlay&) = delete;

        // Shows `content`, replacing an overlay already on screen.
        void Show(OverlayContent content);

        // Closes the overlay and waits for its thread.
        void Close();

    private:
        static void Run(OverlayContent content, HANDLE closeEvent);

        std::thread m_thread;
        HANDLE m_closeEvent = nullptr;
    };
}
