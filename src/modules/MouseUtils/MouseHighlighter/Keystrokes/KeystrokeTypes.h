// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Core value types for the Input Highlighter keystroke overlay. These are pure
// (no WinRT/UI dependencies) so the formatter and processor can be unit tested.
#pragma once

#include <array>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <string>

namespace InputHighlighter
{
    // Indices into KeystrokeEvent::mods (order matches the keyboard snapshot).
    enum ModifierIndex
    {
        Mod_Ctrl = 0,
        Mod_Alt = 1,
        Mod_Shift = 2,
        Mod_Win = 3,
    };

    enum class KeystrokeEventType : uint8_t
    {
        Down,
        Up,
    };

    // A single captured keystroke. POD so it can be copied cheaply across the
    // producer (hook thread) / consumer (composition thread) boundary.
    struct KeystrokeEvent
    {
        KeystrokeEventType type = KeystrokeEventType::Down;
        uint32_t vk = 0; // virtual key code
        char32_t ch = 0; // printable character for this key (0 when non-printable)
        std::array<bool, 4> mods = { false, false, false, false }; // Ctrl, Alt, Shift, Win
        uint64_t tsMicros = 0;
    };

    // A keyboard shortcut (modifier chord + trigger key) used to drive overlay
    // control actions from within the keystroke capture hook. Modifier order
    // matches KeystrokeEvent::mods (Ctrl, Alt, Shift, Win).
    struct HotkeyChord
    {
        bool ctrl = false;
        bool alt = false;
        bool shift = false;
        bool win = false;
        uint32_t vk = 0; // 0 = unbound

        bool IsBound() const { return vk != 0; }

        bool Matches(uint32_t downVk, const std::array<bool, 4>& mods) const
        {
            return vk != 0 && vk == downVk &&
                   ctrl == mods[Mod_Ctrl] &&
                   alt == mods[Mod_Alt] &&
                   shift == mods[Mod_Shift] &&
                   win == mods[Mod_Win];
        }
    };

    // Display modes, ported from the team4 KeystrokeOverlay DisplayMode enum.
    enum class DisplayMode
    {
        Last5 = 0,
        SingleCharactersOnly = 1,
        ShortcutsOnly = 2,
        Stream = 3,
    };

    enum class KeystrokeAction
    {
        None, // Do nothing
        Add, // Create a new visual "pill"
        ReplaceLast, // Update the current pill (e.g. "Hell" -> "Hello")
        RemoveLast, // Backspace an entire pill
    };

    struct KeystrokeResult
    {
        KeystrokeAction action = KeystrokeAction::None;
        std::wstring text;
    };

    // Single-producer / single-consumer lock-free ring buffer. The producer is the
    // low-level keyboard hook thread; the consumer is the composition thread.
    template<typename T, size_t N>
    class SpscRing
    {
    public:
        bool try_push(const T& v)
        {
            const auto head = _head.load(std::memory_order_relaxed);
            const auto next = (head + 1) % N;
            if (next == _tail.load(std::memory_order_acquire))
            {
                return false; // full
            }
            _buf[head] = v;
            _head.store(next, std::memory_order_release);
            return true;
        }

        bool try_pop(T& out)
        {
            const auto tail = _tail.load(std::memory_order_relaxed);
            if (tail == _head.load(std::memory_order_acquire))
            {
                return false; // empty
            }
            out = _buf[tail];
            _tail.store((tail + 1) % N, std::memory_order_release);
            return true;
        }

    private:
        std::array<T, N> _buf{};
        std::atomic<size_t> _head{ 0 };
        std::atomic<size_t> _tail{ 0 };
    };
}
