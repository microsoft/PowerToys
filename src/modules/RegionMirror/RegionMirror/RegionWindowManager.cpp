// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "RegionWindowManager.h"
#include "WindowManagementLogic.h"

#include <dwmapi.h>

#include <array>
#include <cstdlib>
#include <stdexcept>
#include <system_error>
#include <unordered_map>
#include <vector>

namespace RegionMirror
{
    namespace
    {
        constexpr wchar_t ManagerClass[] = L"PowerToys.RegionMirror.WindowManager";
        constexpr UINT_PTR FitTimer = 1;
        constexpr UINT TimerIntervalMs = 50;
        constexpr ULONGLONG VerificationDelayMs = 150;
        constexpr unsigned MaximumAttempts = 3;
        constexpr size_t MaximumTrackedWindows = 256;

        class PhysicalCoordinates final
        {
        public:
            PhysicalCoordinates() noexcept :
                m_previous(SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
            {
            }

            ~PhysicalCoordinates()
            {
                if (m_previous)
                {
                    SetThreadDpiAwarenessContext(m_previous);
                }
            }

        private:
            DPI_AWARENESS_CONTEXT m_previous;
        };

        bool NearRectangle(const RECT& first, const RECT& second) noexcept
        {
            return std::abs(static_cast<int64_t>(first.left) - second.left) <= 1 &&
                   std::abs(static_cast<int64_t>(first.top) - second.top) <= 1 &&
                   std::abs(static_cast<int64_t>(first.right) - second.right) <= 1 &&
                   std::abs(static_cast<int64_t>(first.bottom) - second.bottom) <= 1;
        }

        bool WindowRectangles(HWND window, RECT& outer, RECT& visible) noexcept
        {
            if (!GetWindowRect(window, &outer))
            {
                return false;
            }
            if (FAILED(DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &visible, sizeof(visible))) ||
                visible.left >= visible.right || visible.top >= visible.bottom)
            {
                visible = outer;
            }
            return outer.left < outer.right && outer.top < outer.bottom;
        }
    }

    struct RegionWindowManager::State
    {
        struct Entry
        {
            DWORD process{};
            DWORD thread{};
            bool managed{};
            bool dragging{};
            bool wasMaximized{};
            bool pending{};
            bool issuedResize{};
            unsigned attempts{};
            ULONGLONG nextAttempt{};
            uint64_t generation{};
        };

        RECT region{};
        HWND window{};
        DWORD ownerThread = GetCurrentThreadId();
        DWORD ownProcess = GetCurrentProcessId();
        std::vector<HWINEVENTHOOK> hooks;
        std::unordered_map<HWND, Entry> entries;
        bool timerRunning{};
        bool stopping{};
        uint64_t nextGeneration{};
        uint64_t adjusted{};
        std::wstring lastError;

        ~State() { Stop(); }

        // OUTOFCONTEXT callbacks are delivered on the registering thread. Erase
        // entries before unhooking so a late notification cannot reach freed state.
        static auto& HookOwners()
        {
            static thread_local std::unordered_map<HWINEVENTHOOK, State*> owners;
            return owners;
        }

        void RecordError(const wchar_t* message) noexcept
        {
            try
            {
                lastError = message;
            }
            catch (...)
            {
                // Never unwind an allocation failure through a Windows callback.
            }
        }

        bool HasIdentity(HWND target, const Entry& entry) const noexcept
        {
            DWORD process{};
            const auto thread = GetWindowThreadProcessId(target, &process);
            return thread && thread == entry.thread && process == entry.process && process != ownProcess;
        }

        bool Eligible(HWND target, DWORD& process, DWORD& thread) const noexcept
        {
            if (!target || target == GetDesktopWindow() || target == GetShellWindow() ||
                !IsWindowVisible(target) || GetAncestor(target, GA_ROOT) != target)
            {
                return false;
            }
            thread = GetWindowThreadProcessId(target, &process);
            if (!thread || !process || process == ownProcess)
            {
                return false;
            }
            const auto style = static_cast<DWORD>(GetWindowLongPtrW(target, GWL_STYLE));
            const auto extended = static_cast<DWORD>(GetWindowLongPtrW(target, GWL_EXSTYLE));
            if ((style & WS_CHILD) || !(style & WS_THICKFRAME) || !(style & WS_MAXIMIZEBOX) ||
                (extended & (WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)))
            {
                return false;
            }
            DWORD cloaked{};
            return FAILED(DwmGetWindowAttribute(target, DWMWA_CLOAKED, &cloaked, sizeof(cloaked))) || !cloaked;
        }

        void Initialize(RECT selectedRegion)
        {
            PhysicalCoordinates physical;
            region = selectedRegion;
            WNDCLASSW definition{};
            definition.lpfnWndProc = WindowProc;
            definition.hInstance = GetModuleHandleW(nullptr);
            definition.lpszClassName = ManagerClass;
            if (!RegisterClassW(&definition) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
            {
                throw std::system_error(static_cast<int>(GetLastError()), std::system_category(), "Unable to register the window manager.");
            }
            window = CreateWindowExW(0, ManagerClass, L"", 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, definition.hInstance, this);
            if (!window)
            {
                throw std::system_error(static_cast<int>(GetLastError()), std::system_category(), "Unable to create the window manager.");
            }
            constexpr std::array<DWORD, 5> events{
                EVENT_SYSTEM_MOVESIZESTART,
                EVENT_SYSTEM_MOVESIZEEND,
                EVENT_OBJECT_LOCATIONCHANGE,
                EVENT_OBJECT_STATECHANGE,
                EVENT_OBJECT_DESTROY
            };
            hooks.reserve(events.size());
            for (const auto event : events)
            {
                const auto hook = SetWinEventHook(event, event, nullptr, WinEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                if (!hook)
                {
                    throw std::system_error(static_cast<int>(GetLastError()), std::system_category(), "Unable to subscribe to window events.");
                }
                hooks.push_back(hook);
                HookOwners().emplace(hook, this);
            }
        }

        void Stop() noexcept
        {
            if (stopping)
            {
                return;
            }
            stopping = true;
            for (const auto hook : hooks)
            {
                HookOwners().erase(hook);
                UnhookWinEvent(hook);
            }
            hooks.clear();
            if (window)
            {
                KillTimer(window, FitTimer);
                DestroyWindow(window);
                window = nullptr;
            }
            timerRunning = false;
            entries.clear();
            stopping = false;
            // Keep adjusted and lastError available for the final session report.
        }

        void CancelFit(Entry& entry) noexcept
        {
            entry.pending = false;
            entry.attempts = 0;
            entry.issuedResize = false;
            entry.generation = ++nextGeneration;
        }

        void ScheduleFit(Entry& entry)
        {
            CancelFit(entry);
            entry.pending = true;
            entry.nextAttempt = GetTickCount64() + TimerIntervalMs;
            if (!timerRunning)
            {
                timerRunning = SetTimer(window, FitTimer, TimerIntervalMs, nullptr) != 0;
                if (!timerRunning)
                {
                    entry.pending = false;
                    RecordError(L"Unable to schedule the maximized-window adjustment.");
                }
            }
        }

        void HandleEvent(DWORD event, HWND target, LONG object, LONG child, DWORD eventThread)
        {
            if (stopping || !target || object != OBJID_WINDOW || child != CHILDID_SELF)
            {
                return;
            }
            auto found = entries.find(target);
            if (event == EVENT_OBJECT_DESTROY)
            {
                if (found != entries.end())
                {
                    entries.erase(found);
                }
                return;
            }
            if (found != entries.end() && !HasIdentity(target, found->second))
            {
                entries.erase(found);
                found = entries.end();
            }

            if (event == EVENT_SYSTEM_MOVESIZESTART || event == EVENT_SYSTEM_MOVESIZEEND)
            {
                DWORD process{};
                DWORD thread{};
                // Only the target UI thread can enroll its move/size gesture.
                const bool eligible = Eligible(target, process, thread) && eventThread == thread;
                found = entries.find(target);
                if (!eligible)
                {
                    if (found != entries.end())
                    {
                        entries.erase(found);
                    }
                    return;
                }
                if (found != entries.end() && (found->second.process != process || found->second.thread != thread))
                {
                    entries.erase(found);
                    found = entries.end();
                }
                if (found == entries.end())
                {
                    if (entries.size() >= MaximumTrackedWindows)
                    {
                        RecordError(L"The region window manager reached its 256-window tracking limit.");
                        return;
                    }
                    Entry initial;
                    initial.process = process;
                    initial.thread = thread;
                    initial.wasMaximized = IsZoomed(target) != FALSE;
                    found = entries.emplace(target, initial).first;
                }
                auto& entry = found->second;
                CancelFit(entry);
                entry.wasMaximized = IsZoomed(target) != FALSE;
                entry.dragging = event == EVENT_SYSTEM_MOVESIZESTART;
                if (entry.dragging)
                {
                    return;
                }

                POINT cursor{};
                RECT outer{};
                RECT visible{};
                RECT intersection{};
                const auto generation = entry.generation;
                const bool inside = GetCursorPos(&cursor) && PtInRect(&region, cursor) &&
                                    WindowRectangles(target, outer, visible) && IntersectRect(&intersection, &region, &visible);
                found = entries.find(target);
                if (found == entries.end() || found->second.generation != generation || !HasIdentity(target, found->second))
                {
                    return;
                }
                auto& completed = found->second;
                if (!inside)
                {
                    entries.erase(found);
                }
                else if (!completed.wasMaximized && !IsIconic(target))
                {
                    completed.managed = true;
                }
                else if (!completed.managed)
                {
                    entries.erase(found);
                }
                return;
            }

            if (found == entries.end() || !found->second.managed)
            {
                return;
            }
            auto& entry = found->second;
            if (IsIconic(target))
            {
                CancelFit(entry);
                return;
            }

            // Observe the edge in the event callback, before scheduling any work.
            // Re-reading only from a timer could miss a fast maximize/restore pair.
            const bool maximized = IsZoomed(target) != FALSE;
            const bool transition = ObserveMaximized(entry.wasMaximized, maximized, entry.dragging);
            if (!maximized || entry.dragging)
            {
                CancelFit(entry);
            }
            else if (transition)
            {
                ScheduleFit(entry);
            }
        }

        void ProcessFit(HWND target)
        {
            auto found = entries.find(target);
            if (found == entries.end() || !found->second.pending)
            {
                return;
            }
            const auto now = GetTickCount64();
            if (found->second.nextAttempt > now)
            {
                return;
            }
            DWORD process{};
            DWORD thread{};
            const Entry observed = found->second;
            const bool eligible = HasIdentity(target, observed) && Eligible(target, process, thread);
            found = entries.find(target);
            if (found == entries.end() || found->second.generation != observed.generation)
            {
                return;
            }
            if (!eligible || found->second.process != process || found->second.thread != thread)
            {
                entries.erase(found);
                return;
            }
            if (found->second.dragging || IsIconic(target) || !IsZoomed(target))
            {
                CancelFit(found->second);
                return;
            }
            RECT outer{};
            RECT visible{};
            const bool measured = WindowRectangles(target, outer, visible);
            found = entries.find(target);
            if (found == entries.end() || found->second.generation != observed.generation || !HasIdentity(target, observed))
            {
                return;
            }
            if (found->second.dragging || IsIconic(target) || !IsZoomed(target))
            {
                CancelFit(found->second);
                return;
            }
            if (measured && NearRectangle(visible, region))
            {
                if (found->second.issuedResize)
                {
                    ++adjusted;
                }
                CancelFit(found->second);
                return;
            }
            if (found->second.attempts >= MaximumAttempts)
            {
                CancelFit(found->second);
                RecordError(L"A window did not accept the selected region while maximized after three attempts. Its normal placement was not overridden.");
                return;
            }
            const auto bounds = measured ? ComputeOuterBoundsForVisibleRect(region, outer, visible) : std::nullopt;
            ++found->second.attempts;
            found->second.nextAttempt = now + VerificationDelayMs;
            if (!bounds)
            {
                return;
            }

            // Update state before the external call. Any notification it causes
            // sees the existing maximized state and cannot queue another fit.
            const auto generation = found->second.generation;
            const Entry identity = found->second;
            UINT flags = SWP_ASYNCWINDOWPOS | SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOOWNERZORDER;
            if (found->second.attempts > 1)
            {
                // Some windows rewrite maximized bounds in WM_WINDOWPOSCHANGING.
                // Only bounded retries skip that callback; WM_WINDOWPOSCHANGED
                // still lets the application lay out and repaint the new size.
                flags |= SWP_NOSENDCHANGING;
            }
            const bool accepted = SetWindowPos(target, nullptr, bounds->left, bounds->top, bounds->right - bounds->left, bounds->bottom - bounds->top, flags) != FALSE;
            found = entries.find(target);
            if (found == entries.end() || !HasIdentity(target, identity) || found->second.generation != generation)
            {
                return;
            }
            if (accepted)
            {
                found->second.issuedResize = true;
            }
            else
            {
                CancelFit(found->second);
                RecordError(L"Windows rejected an external maximized-window adjustment. The window may require different permissions or may not support this operation.");
            }
        }

        void ProcessWork()
        {
            if (stopping || !window)
            {
                return;
            }
            std::vector<HWND> pending;
            pending.reserve(entries.size());
            for (const auto& [target, entry] : entries)
            {
                if (entry.pending)
                {
                    pending.push_back(target);
                }
            }
            // Snapshot handles so notifications raised by an external API cannot
            // invalidate an iterator over the manager's tracking table.
            for (const auto target : pending)
            {
                ProcessFit(target);
            }
            if (stopping || !window)
            {
                return;
            }
            for (const auto& [target, entry] : entries)
            {
                if (entry.pending)
                {
                    return;
                }
            }
            KillTimer(window, FitTimer);
            timerRunning = false;
        }

        static void CALLBACK WinEventProc(HWINEVENTHOOK hook, DWORD event, HWND target, LONG object, LONG child, DWORD eventThread, DWORD) noexcept
        {
            const auto owner = HookOwners().find(hook);
            if (owner == HookOwners().end())
            {
                return;
            }
            auto* state = owner->second;
            if (state->ownerThread != GetCurrentThreadId())
            {
                return;
            }
            try
            {
                PhysicalCoordinates physical;
                state->HandleEvent(event, target, object, child, eventThread);
            }
            catch (...)
            {
                state->RecordError(L"Unable to process a window-management notification.");
            }
        }

        static LRESULT CALLBACK WindowProc(HWND target, UINT message, WPARAM wParam, LPARAM lParam) noexcept
        {
            auto* state = reinterpret_cast<State*>(GetWindowLongPtrW(target, GWLP_USERDATA));
            if (message == WM_NCCREATE)
            {
                state = static_cast<State*>(reinterpret_cast<CREATESTRUCTW*>(lParam)->lpCreateParams);
                SetWindowLongPtrW(target, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
            }
            if (message == WM_NCDESTROY)
            {
                SetWindowLongPtrW(target, GWLP_USERDATA, 0);
            }
            if (state && !state->stopping && message == WM_TIMER && wParam == FitTimer && state->timerRunning)
            {
                try
                {
                    PhysicalCoordinates physical;
                    state->ProcessWork();
                }
                catch (...)
                {
                    for (auto& [window, entry] : state->entries)
                    {
                        state->CancelFit(entry);
                    }
                    KillTimer(target, FitTimer);
                    state->timerRunning = false;
                    state->RecordError(L"Unable to complete a maximized-window adjustment.");
                }
                return 0;
            }
            return DefWindowProcW(target, message, wParam, lParam);
        }
    };

    RegionWindowManager::RegionWindowManager() = default;

    RegionWindowManager::~RegionWindowManager()
    {
        Stop();
    }

    void RegionWindowManager::Start(RECT region)
    {
        Stop();
        if (!IsValidRegion(region))
        {
            throw std::invalid_argument("The managed region must have valid physical screen bounds.");
        }
        auto state = std::make_unique<State>();
        state->Initialize(region);
        m_state = std::move(state);
    }

    void RegionWindowManager::Stop() noexcept
    {
        if (m_state)
        {
            m_state->Stop();
        }
    }

    size_t RegionWindowManager::ManagedCount() const noexcept
    {
        size_t count{};
        if (m_state)
        {
            for (const auto& [window, entry] : m_state->entries)
            {
                count += entry.managed ? 1 : 0;
            }
        }
        return count;
    }

    uint64_t RegionWindowManager::AdjustedCount() const noexcept
    {
        return m_state ? m_state->adjusted : 0;
    }

    std::wstring RegionWindowManager::LastError() const
    {
        return m_state ? m_state->lastError : std::wstring{};
    }
}
