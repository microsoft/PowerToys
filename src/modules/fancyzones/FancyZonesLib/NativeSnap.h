#pragma once

#include <windows.h>

namespace NativeSnap
{
    // The Shell only applies a snap request when the target rectangle shares at least two edges
    // with the monitor work area. The tolerance covers rounding while zones are scaled.
    constexpr LONG EdgeTolerance = 2;

    // Moves every edge of `rect` that is within `maxEdgeInset` of the matching `workArea` edge
    // onto that edge. Zones of the built-in layouts are inset from the work area by the configured
    // zone spacing, so a zone that sits on the monitor edge does not touch it geometrically.
    // Returns `rect` unchanged when there is no edge to extend.
    RECT ExtendToWorkAreaEdges(const RECT& rect, const RECT& workArea, LONG maxEdgeInset) noexcept;

    // Puts `window` into the Windows snap state for `snapRect` (absolute screen coordinates) so
    // that it can join snap groups. Returns false when native snapping is unavailable or not
    // applicable, and the caller has to fall back to its own window placement.
    bool SnapWindowToRect(HWND window, const RECT& snapRect) noexcept;

    // The Shell applies a snap asynchronously: SnapWindow returns before the window has been
    // moved, so any work done right after it races the Shell. Waits until the snap has visibly
    // been applied (the window is arranged and sits at `snapRect`) or the timeout elapses.
    // Returns true when the snap was observed.
    bool WaitForSnapApplied(HWND window, const RECT& snapRect, DWORD timeoutMs) noexcept;
}
