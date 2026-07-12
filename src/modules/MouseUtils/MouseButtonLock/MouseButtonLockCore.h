// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <atomic>
#include <cstdint>

// The per-button ClickLock state machine, deliberately decoupled from Win32 so it can be unit
// tested. The caller (the module's low-level mouse hook) feeds it events with a monotonic
// millisecond tick and the cursor position plus a settings snapshot, and acts on the returned
// decisions. No Win32 calls live here: synthetic button-up injection is behind IButtonUpInjector,
// and the clock is the caller-supplied tick, so tests can drive both deterministically.
namespace mousebuttonlock
{
    enum class MouseButton
    {
        Left,
        Right,
        Middle,
    };

    // A plain point so the core does not need <windows.h>.
    struct PointL
    {
        long x = 0;
        long y = 0;
    };

    struct Settings
    {
        // The left (primary) button is off by default: it is the main interaction button and
        // Windows already ships ClickLock for it, so locking it is strictly opt-in.
        bool lmbEnabled = false;
        bool rmbEnabled = true;
        bool mmbEnabled = false;
        // Matches Windows' built-in ClickLock default (1200 ms). Every field is overwritten from
        // settings in production (SettingsSnapshot), so this default only surfaces in tests.
        int holdDurationMs = 1200;
        bool moveCancelEnabled = true;
        int moveCancelPixels = 5;
    };

    // Abstraction over the synthetic button-up injection (SendInput in production, a recording
    // fake in tests). Returns true if the OS accepted the synthetic event.
    struct IButtonUpInjector
    {
        virtual ~IButtonUpInjector() = default;
        virtual bool InjectUp(MouseButton button) = 0;
    };

    class Engine
    {
    public:
        explicit Engine(IButtonUpInjector& injector) :
            m_injector(injector)
        {
        }

        Engine(const Engine&) = delete;
        Engine& operator=(const Engine&) = delete;

        // Handle a physical button-down. Returns true if the DOWN should be suppressed.
        bool OnButtonDown(MouseButton button, uint64_t tick, PointL pt, const Settings& s)
        {
            ButtonState& st = State(button);

            // Tap-to-release. exchange() claims the lock atomically so a concurrent release
            // (settings change / shutdown) can't double-act. If we claimed it, inject the
            // synthetic up; on success suppress the DOWN and swallow its paired UP, on failure
            // drop the lock and let the physical events through so the OS can resolve the state.
            if (st.locked.exchange(false))
            {
                if (m_injector.InjectUp(button))
                {
                    st.swallowNextRealUp = true;
                    return true;
                }
                return false;
            }

            if (!Enabled(button, s))
            {
                return false;
            }

            st.physicalDown = true;
            st.moveCancelled = false;
            st.downTick = tick;
            st.downPos = pt;
            return false;
        }

        // Handle a physical button-up. Returns true if the UP should be suppressed (i.e. the
        // button just locked, or this UP is the swallowed pair of a release tap).
        bool OnButtonUp(MouseButton button, uint64_t tick, const Settings& s)
        {
            ButtonState& st = State(button);

            if (st.swallowNextRealUp)
            {
                st.swallowNextRealUp = false;
                return true;
            }

            if (!st.physicalDown)
            {
                return false;
            }
            st.physicalDown = false;

            if (!Enabled(button, s))
            {
                return false;
            }

            const int holdMs = s.holdDurationMs < 0 ? 0 : s.holdDurationMs;
            const uint64_t elapsed = tick - st.downTick;
            if (!st.moveCancelled && elapsed >= static_cast<uint64_t>(holdMs))
            {
                st.locked.store(true);
                return true;
            }
            return false;
        }

        // Handle cursor movement (never suppressed). Cancels an in-progress hold if the cursor
        // leaves the dead-zone before the threshold elapses; once armed, motion no longer cancels.
        void OnMove(uint64_t tick, PointL pt, const Settings& s)
        {
            if (!s.moveCancelEnabled)
            {
                return;
            }
            const int holdMs = s.holdDurationMs < 0 ? 0 : s.holdDurationMs;
            const int pixels = s.moveCancelPixels < 0 ? 0 : s.moveCancelPixels;
            CheckMoveCancel(m_left, tick, holdMs, pixels, pt);
            CheckMoveCancel(m_right, tick, holdMs, pixels, pt);
            CheckMoveCancel(m_middle, tick, holdMs, pixels, pt);
        }

        // Release any button whose lock has just been turned off in settings.
        void EnforceEnabled(const Settings& s)
        {
            if (!s.lmbEnabled)
            {
                ReleaseButton(m_left, MouseButton::Left);
            }
            if (!s.rmbEnabled)
            {
                ReleaseButton(m_right, MouseButton::Right);
            }
            if (!s.mmbEnabled)
            {
                ReleaseButton(m_middle, MouseButton::Middle);
            }
        }

        // Release every locked button (crash/shutdown safety).
        void ReleaseAll()
        {
            ReleaseButton(m_left, MouseButton::Left);
            ReleaseButton(m_right, MouseButton::Right);
            ReleaseButton(m_middle, MouseButton::Middle);
        }

        // Clear transient hold state. Call when (re)enabling so a button held across a
        // disable/enable cycle can't produce a spurious lock or a swallowed later click.
        void ResetTransient()
        {
            ResetOne(m_left);
            ResetOne(m_right);
            ResetOne(m_middle);
        }

        bool IsLocked(MouseButton button) const
        {
            return State(button).locked.load();
        }

    private:
        struct ButtonState
        {
            bool physicalDown = false;
            bool moveCancelled = false;
            bool swallowNextRealUp = false;
            uint64_t downTick = 0;
            PointL downPos{};
            std::atomic<bool> locked{ false };
        };

        ButtonState& State(MouseButton b)
        {
            switch (b)
            {
            case MouseButton::Left:
                return m_left;
            case MouseButton::Middle:
                return m_middle;
            case MouseButton::Right:
            default:
                return m_right;
            }
        }

        const ButtonState& State(MouseButton b) const
        {
            switch (b)
            {
            case MouseButton::Left:
                return m_left;
            case MouseButton::Middle:
                return m_middle;
            case MouseButton::Right:
            default:
                return m_right;
            }
        }

        static bool Enabled(MouseButton b, const Settings& s)
        {
            switch (b)
            {
            case MouseButton::Left:
                return s.lmbEnabled;
            case MouseButton::Middle:
                return s.mmbEnabled;
            case MouseButton::Right:
            default:
                return s.rmbEnabled;
            }
        }

        static void CheckMoveCancel(ButtonState& st, uint64_t now, int holdMs, int pixels, PointL pt)
        {
            if (!st.physicalDown || st.locked.load() || st.moveCancelled)
            {
                return;
            }
            if (now - st.downTick >= static_cast<uint64_t>(holdMs))
            {
                return;
            }
            // Compare squared distance in double. pt coordinates are 32-bit, so a raw long long
            // product (dx*dx + dy*dy) can overflow signed 64-bit for extreme inputs. In production
            // the cursor is screen-bounded so this never triggers, but the engine must stay defined
            // for any input, and the fuzz target drives the full coordinate range. double holds these
            // magnitudes without overflow; precision is far finer than a pixel dead-zone needs.
            const double dx = static_cast<double>(pt.x) - static_cast<double>(st.downPos.x);
            const double dy = static_cast<double>(pt.y) - static_cast<double>(st.downPos.y);
            const double threshold = static_cast<double>(pixels) * static_cast<double>(pixels);
            if (dx * dx + dy * dy > threshold)
            {
                st.moveCancelled = true;
            }
        }

        void ReleaseButton(ButtonState& st, MouseButton button)
        {
            // exchange() claims the lock atomically so among racing releasers exactly one injects.
            if (st.locked.exchange(false))
            {
                m_injector.InjectUp(button);
            }
        }

        static void ResetOne(ButtonState& st)
        {
            st.physicalDown = false;
            st.moveCancelled = false;
            st.swallowNextRealUp = false;
            st.downTick = 0;
        }

        IButtonUpInjector& m_injector;
        ButtonState m_left;
        ButtonState m_right;
        ButtonState m_middle;
    };
}
