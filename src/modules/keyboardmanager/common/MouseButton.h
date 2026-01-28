#pragma once

#include <Windows.h>
#include <string>
#include <optional>

// Enum representing mouse buttons that can be remapped
enum class MouseButton : DWORD
{
    Left = 0,
    Right = 1,
    Middle = 2,
    X1 = 3,  // Back button
    X2 = 4,  // Forward button
    ScrollUp = 5,
    ScrollDown = 6
};

namespace MouseButtonHelpers
{
    // Convert WM_* message and mouseData to MouseButton enum
    // Returns nullopt if the message is not a supported mouse button event
    inline std::optional<MouseButton> MouseButtonFromMessage(WPARAM wParam, DWORD mouseData)
    {
        switch (wParam)
        {
        case WM_LBUTTONDOWN:
        case WM_LBUTTONUP:
            return MouseButton::Left;
        case WM_RBUTTONDOWN:
        case WM_RBUTTONUP:
            return MouseButton::Right;
        case WM_MBUTTONDOWN:
        case WM_MBUTTONUP:
            return MouseButton::Middle;
        case WM_XBUTTONDOWN:
        case WM_XBUTTONUP:
            // HIWORD of mouseData contains XBUTTON1 or XBUTTON2
            if (HIWORD(mouseData) == XBUTTON1)
                return MouseButton::X1;
            else if (HIWORD(mouseData) == XBUTTON2)
                return MouseButton::X2;
            return std::nullopt;
        case WM_MOUSEWHEEL:
            // HIWORD of mouseData contains wheel delta (positive = up, negative = down)
            if (static_cast<short>(HIWORD(mouseData)) > 0)
                return MouseButton::ScrollUp;
            else
                return MouseButton::ScrollDown;
        default:
            return std::nullopt;
        }
    }

    // Check if the message is a button down event
    inline bool IsMouseButtonDown(WPARAM wParam)
    {
        switch (wParam)
        {
        case WM_LBUTTONDOWN:
        case WM_RBUTTONDOWN:
        case WM_MBUTTONDOWN:
        case WM_XBUTTONDOWN:
        case WM_MOUSEWHEEL:  // Scroll events are treated as "down" events (one-shot)
            return true;
        default:
            return false;
        }
    }

    // Check if the message is a button up event
    inline bool IsMouseButtonUp(WPARAM wParam)
    {
        switch (wParam)
        {
        case WM_LBUTTONUP:
        case WM_RBUTTONUP:
        case WM_MBUTTONUP:
        case WM_XBUTTONUP:
            return true;
        default:
            return false;
        }
    }

    // Check if a button is a scroll wheel event (no up/down semantics)
    inline constexpr bool IsScrollWheelButton(MouseButton button)
    {
        return button == MouseButton::ScrollUp || button == MouseButton::ScrollDown;
    }

    // Get the display name for a mouse button (for UI)
    inline std::wstring GetMouseButtonName(MouseButton button)
    {
        switch (button)
        {
        case MouseButton::Left:
            return L"Left Button";
        case MouseButton::Right:
            return L"Right Button";
        case MouseButton::Middle:
            return L"Middle Button";
        case MouseButton::X1:
            return L"X1 (Back)";
        case MouseButton::X2:
            return L"X2 (Forward)";
        case MouseButton::ScrollUp:
            return L"Scroll Up";
        case MouseButton::ScrollDown:
            return L"Scroll Down";
        default:
            return L"Unknown";
        }
    }

    // Convert mouse button to string for JSON serialization
    inline std::wstring MouseButtonToString(MouseButton button)
    {
        switch (button)
        {
        case MouseButton::Left:
            return L"Left";
        case MouseButton::Right:
            return L"Right";
        case MouseButton::Middle:
            return L"Middle";
        case MouseButton::X1:
            return L"X1";
        case MouseButton::X2:
            return L"X2";
        case MouseButton::ScrollUp:
            return L"ScrollUp";
        case MouseButton::ScrollDown:
            return L"ScrollDown";
        default:
            return L"";
        }
    }

    // Parse mouse button from string (from JSON)
    inline std::optional<MouseButton> MouseButtonFromString(const std::wstring& str)
    {
        if (str == L"Left")
            return MouseButton::Left;
        else if (str == L"Right")
            return MouseButton::Right;
        else if (str == L"Middle")
            return MouseButton::Middle;
        else if (str == L"X1")
            return MouseButton::X1;
        else if (str == L"X2")
            return MouseButton::X2;
        else if (str == L"ScrollUp")
            return MouseButton::ScrollUp;
        else if (str == L"ScrollDown")
            return MouseButton::ScrollDown;
        return std::nullopt;
    }

    // Get the mouse event flags for SendInput to simulate a button down
    inline DWORD GetMouseDownFlag(MouseButton button)
    {
        switch (button)
        {
        case MouseButton::Left:
            return MOUSEEVENTF_LEFTDOWN;
        case MouseButton::Right:
            return MOUSEEVENTF_RIGHTDOWN;
        case MouseButton::Middle:
            return MOUSEEVENTF_MIDDLEDOWN;
        case MouseButton::X1:
        case MouseButton::X2:
            return MOUSEEVENTF_XDOWN;
        case MouseButton::ScrollUp:
        case MouseButton::ScrollDown:
            return MOUSEEVENTF_WHEEL;
        default:
            return 0;
        }
    }

    // Get the mouse event flags for SendInput to simulate a button up
    inline DWORD GetMouseUpFlag(MouseButton button)
    {
        switch (button)
        {
        case MouseButton::Left:
            return MOUSEEVENTF_LEFTUP;
        case MouseButton::Right:
            return MOUSEEVENTF_RIGHTUP;
        case MouseButton::Middle:
            return MOUSEEVENTF_MIDDLEUP;
        case MouseButton::X1:
        case MouseButton::X2:
            return MOUSEEVENTF_XUP;
        case MouseButton::ScrollUp:
        case MouseButton::ScrollDown:
            return 0;  // Scroll wheel doesn't have up/down events
        default:
            return 0;
        }
    }

    // Get the mouseData value for X buttons and scroll wheel (used with SendInput)
    inline DWORD GetXButtonData(MouseButton button)
    {
        if (button == MouseButton::X1)
            return XBUTTON1;
        else if (button == MouseButton::X2)
            return XBUTTON2;
        else if (button == MouseButton::ScrollUp)
            return static_cast<DWORD>(WHEEL_DELTA);  // Positive for scroll up
        else if (button == MouseButton::ScrollDown)
            return static_cast<DWORD>(-WHEEL_DELTA);  // Negative for scroll down
        return 0;
    }
}

// Hash function for MouseButton to use in unordered_map
namespace std
{
    template <>
    struct hash<MouseButton>
    {
        size_t operator()(const MouseButton& button) const noexcept
        {
            return hash<DWORD>()(static_cast<DWORD>(button));
        }
    };
}
