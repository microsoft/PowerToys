// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <algorithm>
#include <cstdint>

namespace auto_hide_cursor
{
    constexpr std::uint32_t defaultIdleDelayMs = 5000;
    constexpr std::uint32_t minimumIdleDelayMs = 1000;
    constexpr std::uint32_t maximumIdleDelayMs = 60000;
    constexpr std::int64_t movementThresholdPixels = 2;

    struct Point
    {
        long x;
        long y;
    };

    struct Configuration
    {
        bool hideOnTyping = true;
        bool hideOnIdle = false;
        std::uint32_t idleDelayMs = defaultIdleDelayMs;
    };

    enum class MouseInputKind
    {
        Move,
        ButtonOrWheel,
    };

    enum class CursorAction
    {
        None,
        Hide,
        Show,
    };

    class State
    {
    public:
        State(Configuration configuration, std::uint64_t now, Point cursorPosition) noexcept :
            m_configuration{ NormalizeConfiguration(configuration) },
            m_lastPointerInputTime{ now },
            m_hiddenPosition{ cursorPosition }
        {
        }

        static constexpr Configuration NormalizeConfiguration(Configuration configuration) noexcept
        {
            configuration.idleDelayMs = std::clamp(
                configuration.idleDelayMs,
                minimumIdleDelayMs,
                maximumIdleDelayMs);
            return configuration;
        }

        static bool IsModifierVirtualKey(std::uint32_t virtualKey) noexcept
        {
            switch (virtualKey)
            {
            case 0x10: // VK_SHIFT
            case 0x11: // VK_CONTROL
            case 0x12: // VK_MENU
            case 0x5B: // VK_LWIN
            case 0x5C: // VK_RWIN
            case 0xA0: // VK_LSHIFT
            case 0xA1: // VK_RSHIFT
            case 0xA2: // VK_LCONTROL
            case 0xA3: // VK_RCONTROL
            case 0xA4: // VK_LMENU
            case 0xA5: // VK_RMENU
                return true;
            default:
                return false;
            }
        }

        CursorAction OnKeyboardInput(std::uint64_t, Point cursorPosition) noexcept
        {
            if (!m_configuration.hideOnTyping || m_hidden)
            {
                return CursorAction::None;
            }

            m_hidden = true;
            m_hiddenPosition = cursorPosition;
            return CursorAction::Hide;
        }

        CursorAction OnMouseInput(std::uint64_t now, Point cursorPosition, MouseInputKind inputKind) noexcept
        {
            m_lastPointerInputTime = now;
            if (!m_hidden)
            {
                return CursorAction::None;
            }

            if (inputKind == MouseInputKind::Move && !HasMovedIntentionally(cursorPosition))
            {
                return CursorAction::None;
            }

            m_hidden = false;
            return CursorAction::Show;
        }

        CursorAction OnTimer(std::uint64_t now, Point cursorPosition) noexcept
        {
            if (!m_configuration.hideOnIdle || m_hidden ||
                now - m_lastPointerInputTime < m_configuration.idleDelayMs)
            {
                return CursorAction::None;
            }

            m_hidden = true;
            m_hiddenPosition = cursorPosition;
            return CursorAction::Hide;
        }

        CursorAction Stop() noexcept
        {
            if (!m_hidden)
            {
                return CursorAction::None;
            }

            m_hidden = false;
            return CursorAction::Show;
        }

        void HideFailed(std::uint64_t now) noexcept
        {
            m_hidden = false;
            m_lastPointerInputTime = now;
        }

        void ShowFailed() noexcept
        {
            m_hidden = true;
        }

        bool IsHidden() const noexcept
        {
            return m_hidden;
        }

    private:
        bool HasMovedIntentionally(Point cursorPosition) const noexcept
        {
            const auto deltaX = static_cast<std::int64_t>(cursorPosition.x) - m_hiddenPosition.x;
            const auto deltaY = static_cast<std::int64_t>(cursorPosition.y) - m_hiddenPosition.y;
            return (deltaX * deltaX) + (deltaY * deltaY) >=
                   movementThresholdPixels * movementThresholdPixels;
        }

        Configuration m_configuration;
        std::uint64_t m_lastPointerInputTime;
        Point m_hiddenPosition;
        bool m_hidden = false;
    };
}
