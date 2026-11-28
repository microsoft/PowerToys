#pragma once

#include <windows.h>

#include <vector>

namespace NativeSnap
{
    // The Shell only applies a snap request when the target rectangle shares at least two edges
    // with the monitor work area. The tolerance covers rounding while zones are scaled.
    constexpr LONG EdgeTolerance = 2;

    // The Shell hosts its own surfaces in island windows, and the Snap Assist flyout is one of them.
    // It used to be recognized by its title, but the Shell localizes that title ("Snap Assist" is
    // "贴靠助手" on a zh-CN system), so no fixed string matches on every machine and the flyout was
    // never dismissed outside English Windows. It is recognized by class and by having appeared
    // since the snap instead.
    using IslandWindows = std::vector<HWND>;

    // Every island window that exists right now, with no filtering. Capture this before sending a
    // snap request and pass it to the dismissal, which looks for one that is not in it.
    IslandWindows CaptureIslandWindows() noexcept;

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
    // Waiting services this thread's sent messages but never dispatches posted ones, so it is safe
    // to call while holding references into the caller's state.
    bool WaitForSnapApplied(HWND window, const RECT& snapRect, DWORD timeoutMs) noexcept;

    // SnapWindowToRect followed by WaitForSnapApplied, repeated up to `maxAttempts` times.
    //
    // The Shell occasionally accepts a snap request and then does nothing with it, without
    // reporting an error: the window neither moves nor becomes arranged, and waiting longer does
    // not help (measured: a snap that has not landed within the timeout never lands). Such a
    // dropped request does land when it is asked for again, while a single long wait just blocks
    // the caller. That is why the wait is short and the request is repeated.
    //
    // A request is only repeated when the window did not move at all. A window that moved
    // somewhere else was placed by the Shell on purpose, and asking again would fight it.
    bool SnapWindowAndWait(HWND window, const RECT& snapRect, DWORD timeoutMs, int maxAttempts) noexcept;

    // Closes the Shell's Snap Assist flyout when it is being shown over the window that was just
    // snapped. FancyZones places windows itself, so the flyout only takes the foreground away from
    // the window the user dropped. `existing` is the set of island windows captured before the
    // snap: whichever island window appears after that and takes the foreground is the flyout.
    // Waits up to `timeoutMs` for it, because the Shell creates and shows it asynchronously.
    // Does nothing when it never appears. Blocks the calling thread until it is gone or the budget
    // runs out.
    void DismissSnapAssist(HWND window, const IslandWindows& existing, DWORD timeoutMs) noexcept;

    // Same as DismissSnapAssist, but waits on its own thread so the caller is not blocked. Use this
    // from code that handles the end of a user drag.
    void DismissSnapAssistInBackground(HWND window, IslandWindows existing, DWORD timeoutMs) noexcept;
}
