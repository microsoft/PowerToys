#include "pch.h"
#include "NativeSnap.h"

#include <algorithm>
#include <cstdlib>
#include <chrono>
#include <condition_variable>
#include <mutex>
#include <roapi.h>
#include <winstring.h>
#include <dwmapi.h>
#include <thread>
#include <utility>

#pragma comment(lib, "dwmapi.lib")

namespace
{
    // WindowsUdk.UI.Shell.SnapLayoutManager is a Shell runtime class that ships with Windows but
    // is not part of the public API: there is no header, no IDL and no compatibility promise.
    // It is invoked through its ABI, declared below.
    // Registration:
    //   HKLM\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\WindowsUdk.UI.Shell.SnapLayoutManager
    //   DllPath = %SystemRoot%\system32\windowsudk.shellcommon.dll
    constexpr wchar_t SnapLayoutManagerClassName[] = L"WindowsUdk.UI.Shell.SnapLayoutManager";
    constexpr UINT32 SnapLayoutManagerClassNameLength = ARRAYSIZE(SnapLayoutManagerClassName) - 1;

    // The Shell hosts its flyouts, Task View and Snap Assist among them, in island windows. Only
    // the class is used to find them: their titles are localized by the Shell, so a fixed string
    // matches at most one language ("Snap Assist" is "贴靠助手" on a zh-CN system).
    constexpr wchar_t SnapAssistWindowClass[] = L"XamlExplorerHostIslandWindow";

    // Finds the Snap Assist flyout, as the island window that is not in `existing`.
    //
    // Task View lives in an island window as well and is present the whole time, so membership in
    // the set captured before the snap is what tells the flyout apart, not its title. The flyout is
    // returned whether or not it has been shown yet: the Shell creates the window before it makes
    // it visible, and that gap is when the dismissal is most effective.
    HWND FindNewIslandWindow(const NativeSnap::IslandWindows& existing) noexcept
    {
        struct Context
        {
            const NativeSnap::IslandWindows* existing = nullptr;
            HWND found = nullptr;
        } context{ &existing, nullptr };

        ::EnumWindows(
            [](HWND candidate, LPARAM parameter) noexcept -> BOOL {
                auto* ctx = reinterpret_cast<Context*>(parameter);

                wchar_t className[64]{};
                if (!::GetClassNameW(candidate, className, ARRAYSIZE(className)) ||
                    wcscmp(className, SnapAssistWindowClass) != 0)
                {
                    return TRUE;
                }

                const auto& set = *ctx->existing;
                if (std::find(set.begin(), set.end(), candidate) != set.end())
                {
                    return TRUE;
                }

                ctx->found = candidate;
                return FALSE;
            },
            reinterpret_cast<LPARAM>(&context));

        return context.found;
    }

    // Makes `window` the foreground window from a process that does not own the foreground.
    // SetForegroundWindow alone is silently refused in that case (measured: it returns true and
    // nothing happens), so the input state is shared with the thread that currently owns the
    // foreground, which is the documented way to move focus deliberately.
    bool BringToForeground(HWND window) noexcept
    {
        const HWND foreground = ::GetForegroundWindow();
        if (foreground == window)
        {
            return true;
        }

        const DWORD foregroundThread = foreground ? ::GetWindowThreadProcessId(foreground, nullptr) : 0;
        const DWORD thisThread = ::GetCurrentThreadId();

        bool attached = false;
        if (foregroundThread && foregroundThread != thisThread)
        {
            attached = ::AttachThreadInput(thisThread, foregroundThread, TRUE) != FALSE;
        }

        ::SetForegroundWindow(window);
        const bool moved = ::GetForegroundWindow() == window;

        if (attached)
        {
            ::AttachThreadInput(thisThread, foregroundThread, FALSE);
        }

        return moved;
    }

    // ISnapLayoutManagerStatics, retrieved with RoGetActivationFactory.
    constexpr GUID IID_SnapLayoutManagerStatics{
        0x3d7594b4,
        0x9974,
        0x547f,
        { 0xbd, 0x9a, 0x9b, 0x40, 0x4e, 0x98, 0xd6, 0x99 }
    };

    // ISnapLayoutManagerStatics2, queried from the statics object, creates a manager per work area.
    constexpr GUID IID_SnapLayoutManagerStatics2{
        0x8afd7659,
        0x7b86,
        0x5a2f,
        { 0x95, 0xfe, 0xbd, 0x4d, 0xab, 0xea, 0x8e, 0xb1 }
    };

    // ABI layout of Windows.Foundation.Rect. Coordinates are absolute screen coordinates, not
    // relative to the work area; verified on monitors with a non-zero origin and with negative
    // coordinates.
    struct RectF
    {
        float x;
        float y;
        float width;
        float height;
    };

    static_assert(sizeof(RectF) == 16, "Windows.Foundation.Rect must be 16 bytes");

    // Only the slots that are used are declared. The slot order follows the WinRT ABI:
    // IUnknown(0-2) + IInspectable(3-5) + interface methods starting at 6.
    // A wrong slot index calls a different method with a mismatched signature, which crashes the
    // host process instead of returning an error.
    struct IUnknownVtbl
    {
        HRESULT(__stdcall* QueryInterface)(void* self, const GUID& iid, void** result);
        ULONG(__stdcall* AddRef)(void* self);
        ULONG(__stdcall* Release)(void* self);
    };

    // ISnapLayoutManagerStatics2: CreateForWorkAreaRect is at slot 6.
    struct ISnapLayoutManagerStatics2Vtbl
    {
        void* QueryInterface;
        void* AddRef;
        void* Release;
        void* GetIids;
        void* GetRuntimeClassName;
        void* GetTrustLevel;
        HRESULT(__stdcall* CreateForWorkAreaRect)(void* self, const RectF& workArea, void** result);
    };

    // ISnapLayoutManager: slot 6 is an unused method, SnapWindow is at slot 7.
    // The signature of slot 6 is unknown, so it is only reserved here and never called.
    struct ISnapLayoutManagerVtbl
    {
        void* QueryInterface;
        void* AddRef;
        void* Release;
        void* GetIids;
        void* GetRuntimeClassName;
        void* GetTrustLevel;
        void* UnusedSlot6;
        HRESULT(__stdcall* SnapWindow)(void* self, UINT64 windowId, const RectF& position, UINT32 positionCount, const RectF* positions);
    };

    void ReleaseInterface(void* object) noexcept
    {
        if (object)
        {
            static_cast<IUnknownVtbl*>(*static_cast<void**>(object))->Release(object);
        }
    }

    RectF ToRectF(const RECT& rect) noexcept
    {
        return RectF{
            static_cast<float>(rect.left),
            static_cast<float>(rect.top),
            static_cast<float>(rect.right - rect.left),
            static_cast<float>(rect.bottom - rect.top),
        };
    }

    // The Shell accepts a request whose rectangle does not touch the work area but then does
    // nothing, so the geometry is validated here to tell "silently ignored" from "snapped".
    bool TouchesEnoughWorkAreaEdges(const RECT& workArea, const RECT& rect) noexcept
    {
        int edges = 0;
        if (std::abs(rect.left - workArea.left) <= NativeSnap::EdgeTolerance)
        {
            ++edges;
        }
        if (std::abs(rect.top - workArea.top) <= NativeSnap::EdgeTolerance)
        {
            ++edges;
        }
        if (std::abs(rect.right - workArea.right) <= NativeSnap::EdgeTolerance)
        {
            ++edges;
        }
        if (std::abs(rect.bottom - workArea.bottom) <= NativeSnap::EdgeTolerance)
        {
            ++edges;
        }
        return edges >= 2;
    }

    RECT VisibleFrame(HWND window) noexcept
    {
        RECT current{};
        if (FAILED(::DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &current, sizeof(current))))
        {
            if (!::GetWindowRect(window, &current))
            {
                return RECT{};
            }
        }

        return current;
    }

    bool SameRect(const RECT& left, const RECT& right) noexcept
    {
        return left.left == right.left && left.top == right.top &&
               left.right == right.right && left.bottom == right.bottom;
    }

    // Whether the Shell has finished placing the window on `snapRect`.
    // IsWindowArranged is set by the Shell once the window is part of the snap layout; the geometry
    // check makes sure the Shell applied the rectangle that was requested instead of an older one.
    // The comparison uses the visible frame: GetWindowRect includes the invisible resize border that
    // Windows 11 draws around windows, so it never matches the requested rectangle.
    bool IsSnapApplied(HWND window, const RECT& snapRect) noexcept
    {
        if (!::IsWindowArranged(window))
        {
            return false;
        }

        const RECT current = VisibleFrame(window);
        return std::abs(current.left - snapRect.left) <= NativeSnap::EdgeTolerance &&
               std::abs(current.top - snapRect.top) <= NativeSnap::EdgeTolerance &&
               std::abs(current.right - snapRect.right) <= NativeSnap::EdgeTolerance &&
               std::abs(current.bottom - snapRect.bottom) <= NativeSnap::EdgeTolerance;
    }

    // Waits `milliseconds` without the 15.6 ms granularity of a plain Sleep.
    //
    // The wait loop polls for the Shell to finish, and with the default timer resolution every
    // Sleep(5) actually sleeps one full tick: the observed waits were 0 / 16 / 31 / 32 ms, exact
    // multiples of the tick, while the snap itself had already landed. A high-resolution waitable
    // timer gives ~1 ms precision for this thread only, without raising the resolution for the
    // whole system the way timeBeginPeriod does.
    void PreciseWait(DWORD milliseconds) noexcept
    {
        static HANDLE timer = ::CreateWaitableTimerExW(nullptr,
                                                      nullptr,
                                                      CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
                                                      TIMER_ALL_ACCESS);

        if (!timer)
        {
            ::Sleep(milliseconds);
            return;
        }

        // A negative interval means "relative"; the unit is 100 ns.
        LARGE_INTEGER due{};
        due.QuadPart = -static_cast<LONGLONG>(milliseconds) * 10'000;

        if (!::SetWaitableTimer(timer, &due, 0, nullptr, nullptr, FALSE))
        {
            ::Sleep(milliseconds);
            return;
        }

        ::WaitForSingleObject(timer, milliseconds + 100);
    }

    // Services the messages the Shell sends to this thread and waits a little.
    // MsgWaitForMultipleObjectsEx and Sleep do not service sent messages, and the Shell completes
    // the snap by sending one, so the snap never lands without this.
    // PeekMessage with PM_NOREMOVE services sent messages without removing or dispatching posted
    // ones, which keeps this loop from re-entering the module's window procedure (a display change
    // arriving here would rebuild every work area, destroying the caller's objects while its stack
    // frame is still live).
    void PumpThreadMessages(DWORD milliseconds) noexcept
    {
        MSG message;
        ::PeekMessageW(&message, nullptr, 0, 0, PM_NOREMOVE);
        PreciseWait(milliseconds);
    }
}

NativeSnap::IslandWindows NativeSnap::CaptureIslandWindows() noexcept
{
    IslandWindows islands;

    ::EnumWindows(
        [](HWND candidate, LPARAM parameter) noexcept -> BOOL {
            wchar_t className[64]{};
            if (!::GetClassNameW(candidate, className, ARRAYSIZE(className)) ||
                wcscmp(className, SnapAssistWindowClass) != 0)
            {
                return TRUE;
            }

            reinterpret_cast<IslandWindows*>(parameter)->push_back(candidate);
            return TRUE;
        },
        reinterpret_cast<LPARAM>(&islands));

    return islands;
}

RECT NativeSnap::ExtendToWorkAreaEdges(const RECT& rect, const RECT& workArea, LONG maxEdgeInset) noexcept
{
    RECT extended = rect;
    if (maxEdgeInset < 0)
    {
        return extended;
    }

    if (std::abs(rect.left - workArea.left) <= maxEdgeInset)
    {
        extended.left = workArea.left;
    }
    if (std::abs(rect.top - workArea.top) <= maxEdgeInset)
    {
        extended.top = workArea.top;
    }
    if (std::abs(rect.right - workArea.right) <= maxEdgeInset)
    {
        extended.right = workArea.right;
    }
    if (std::abs(rect.bottom - workArea.bottom) <= maxEdgeInset)
    {
        extended.bottom = workArea.bottom;
    }

    if (extended.right <= extended.left || extended.bottom <= extended.top)
    {
        return rect;
    }

    return extended;
}

bool NativeSnap::SnapWindowToRect(HWND window, const RECT& snapRect) noexcept
{
    if (!window)
    {
        return false;
    }

    // Minimized and hidden windows stay on the SetWindowPlacement path: that path keeps the show
    // command and only changes the restore position, while native snapping would restore the
    // window, which is not what the user expects.
    if (::IsIconic(window) || !::IsWindowVisible(window))
    {
        return false;
    }

    // Non-resizable windows stay on the old path as well: AdjustRectForSizeWindowToRect keeps their
    // size and only moves them, while SnapWindow resizes unconditionally.
    if ((::GetWindowLong(window, GWL_STYLE) & WS_SIZEBOX) == 0)
    {
        return false;
    }

    // The work area is derived from the target rectangle instead of being passed in: for layouts
    // that span multiple monitors the caller's rectangle is the whole virtual screen, which the
    // Shell does not accept as a work area. Zones that span monitors do not touch the edges of a
    // single monitor, so the check below sends them to the fallback path.
    const HMONITOR monitor = ::MonitorFromRect(&snapRect, MONITOR_DEFAULTTONEAREST);
    MONITORINFO monitorInfo{ .cbSize = sizeof(MONITORINFO) };
    if (!monitor || !::GetMonitorInfoW(monitor, &monitorInfo))
    {
        return false;
    }

    if (!TouchesEnoughWorkAreaEdges(monitorInfo.rcWork, snapRect))
    {
        return false;
    }

    // The rectangle has to fit the work area of the monitor it was matched to. A zone that spans
    // several monitors has no matching work area, and the Shell would either ignore it or place
    // the window somewhere unexpected.
    const bool fitsWorkArea =
        snapRect.left >= monitorInfo.rcWork.left - EdgeTolerance &&
        snapRect.top >= monitorInfo.rcWork.top - EdgeTolerance &&
        snapRect.right <= monitorInfo.rcWork.right + EdgeTolerance &&
        snapRect.bottom <= monitorInfo.rcWork.bottom + EdgeTolerance;
    if (!fitsWorkArea)
    {
        return false;
    }

    HSTRING className = nullptr;
    if (FAILED(::WindowsCreateString(SnapLayoutManagerClassName, SnapLayoutManagerClassNameLength, &className)) || !className)
    {
        return false;
    }

    void* statics = nullptr;
    const HRESULT getStaticsResult = ::RoGetActivationFactory(className, IID_SnapLayoutManagerStatics, &statics);
    ::WindowsDeleteString(className);
    if (FAILED(getStaticsResult) || !statics)
    {
        return false;
    }

    void* statics2 = nullptr;
    const HRESULT queryResult = static_cast<IUnknownVtbl*>(*static_cast<void**>(statics))
                                    ->QueryInterface(statics, IID_SnapLayoutManagerStatics2, &statics2);
    ReleaseInterface(statics);
    if (FAILED(queryResult) || !statics2)
    {
        return false;
    }

    void* manager = nullptr;
    const RectF workAreaRect = ToRectF(monitorInfo.rcWork);
    const HRESULT createResult = static_cast<const ISnapLayoutManagerStatics2Vtbl*>(*static_cast<void**>(statics2))
                                     ->CreateForWorkAreaRect(statics2, workAreaRect, &manager);
    ReleaseInterface(statics2);
    if (FAILED(createResult) || !manager)
    {
        return false;
    }

    RectF position = ToRectF(snapRect);
    const HRESULT snapResult = static_cast<const ISnapLayoutManagerVtbl*>(*static_cast<void**>(manager))
                                   ->SnapWindow(
                                       manager,
                                       static_cast<UINT64>(reinterpret_cast<UINT_PTR>(window)),
                                       position,
                                       1,
                                       &position);
    ReleaseInterface(manager);

    return SUCCEEDED(snapResult);
}

bool NativeSnap::WaitForSnapApplied(HWND window, const RECT& snapRect, DWORD timeoutMs) noexcept
{
    if (!window)
    {
        return false;
    }

    const ULONGLONG deadline = ::GetTickCount64() + timeoutMs;
    while (::GetTickCount64() < deadline)
    {
        if (IsSnapApplied(window, snapRect))
        {
            return true;
        }

        PumpThreadMessages(5);
    }

    return IsSnapApplied(window, snapRect);
}

bool NativeSnap::SnapWindowAndWait(HWND window, const RECT& snapRect, DWORD timeoutMs, int maxAttempts) noexcept
{
    if (!window || maxAttempts <= 0)
    {
        return false;
    }

    // The window is remembered before the first request: a drop is a request the Shell accepted
    // without moving anything, and that is only distinguishable from "moved somewhere else" by
    // comparing against where the window was.
    RECT original{};
    if (!::GetWindowRect(window, &original))
    {
        return false;
    }

    for (int attempt = 1; attempt <= maxAttempts; ++attempt)
    {
        if (!SnapWindowToRect(window, snapRect))
        {
            return false;
        }

        if (WaitForSnapApplied(window, snapRect, timeoutMs))
        {
            return true;
        }

        if (attempt == maxAttempts)
        {
            break;
        }

        // A window that moved but did not land on the requested rectangle was placed by the Shell
        // on purpose (a different snap layout, for instance). Repeating the request would fight it,
        // so only a window that has not moved at all is retried.
        RECT current{};
        if (!::GetWindowRect(window, &current) || !SameRect(current, original))
        {
            return false;
        }
    }

    return false;
}

void NativeSnap::DismissSnapAssist(HWND window, const IslandWindows& existing, DWORD timeoutMs) noexcept
{
    if (!window)
    {
        return;
    }

    // Snap Assist is the Shell flyout that offers a window for the space next to a snap. FancyZones
    // places windows on its own, so the offer is not meant for this workflow, and the flyout takes
    // the foreground away from the window the user just dropped.
    //
    // It cannot be kept from being created, and it ignores every closing message: WM_CLOSE,
    // SC_CLOSE and SW_HIDE all leave it on screen (measured). What dismisses it is taking the
    // foreground back, which is what clicking outside of it does.
    //
    // The trigger is the flyout holding the foreground. Dismissing it earlier does not work: before
    // it exists the Shell shows it anyway, and while it exists but has not been shown yet, taking
    // the foreground only postpones it by about half a second. Dismissing it the moment it holds
    // the foreground closes it within ~30 ms, so what remains is the flyout's own opening
    // animation, which is the best available outcome through the supported means.
    //
    // The flyout is recognized by having appeared since the snap, not by its title: the Shell
    // localizes that title, so a fixed string only works on one language.
    //
    // Running this on the caller's thread would block it for up to a second, and the caller handles
    // the end of the user's drag, so callers normally use DismissSnapAssistInBackground instead.
    const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMs);
    bool dismissed = false;

    while (std::chrono::steady_clock::now() < deadline)
    {
        const HWND flyout = FindNewIslandWindow(existing);
        if (!flyout)
        {
            if (dismissed)
            {
                // Gone after a dismissal: the flyout was closed, nothing left to wait for.
                return;
            }
        }
        else if (::GetForegroundWindow() == flyout)
        {
            // Dismissal fails while the Shell is still busy with the snap, so it is retried until
            // the flyout gives up the foreground or the budget runs out.
            BringToForeground(window);
            dismissed = true;
        }

        ::Sleep(5);
    }
}

void NativeSnap::DismissSnapAssistInBackground(HWND window, IslandWindows existing, DWORD timeoutMs) noexcept
{
    if (!window)
    {
        return;
    }

    // The flyout can be created a long time after the snap and only takes the foreground after
    // that, which is up to a second. The caller is in the middle of handling the end of a drag, so
    // waiting there would freeze the window the user just dropped. The wait is therefore moved off
    // the caller's thread.
    //
    // One worker serves every snap: it is started on the first request, picks up the newest window
    // on each pass, and exits once it has been idle, so dragging several windows into zones in
    // quick succession does not pile up one thread per snap.
    struct Pending
    {
        HWND window = nullptr;
        IslandWindows existing{};
        DWORD timeout = 0;
    };

    static std::mutex mutex;
    static std::condition_variable wake;
    static Pending pending{};
    static bool workerRunning = false;

    {
        std::lock_guard lock(mutex);
        // The newest snap wins: the older one's flyout, if any, belongs to a window the user has
        // already moved on from, and BringToForeground would take the foreground from the new one.
        pending.window = window;
        pending.existing = std::move(existing);
        pending.timeout = timeoutMs;

        if (workerRunning)
        {
            // An idle worker is waiting for this; a busy one picks it up when it comes around.
            wake.notify_one();
            return;
        }
        workerRunning = true;
    }

    std::thread([]() noexcept {
        for (;;)
        {
            Pending work{};
            {
                std::unique_lock lock(mutex);
                if (!pending.window)
                {
                    // Idle: give a following snap a chance to reuse this thread, then stop.
                    wake.wait_for(lock, std::chrono::seconds(1), [] {
                        return pending.window != nullptr;
                    });
                }

                work = std::move(pending);
                pending = Pending{};

                if (!work.window)
                {
                    workerRunning = false;
                    return;
                }
            }

            DismissSnapAssist(work.window, work.existing, work.timeout);
        }
    }).detach();
}
