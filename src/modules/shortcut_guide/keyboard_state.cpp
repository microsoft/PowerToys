#include "pch.h"
#include "keyboard_state.h"

bool winkey_held()
{
    auto left = GetAsyncKeyState(VK_LWIN);
    auto right = GetAsyncKeyState(VK_RWIN);
    return (left & 0x8000) || (right & 0x8000);
}

// Returns true if the VK code is in the range of valid keys.
// Some VK codes should not be checked because they would return
// false positives when checking if only the "Win" key is pressed.
constexpr bool should_check(int vk)
{
    switch (vk)
    {
    case VK_CANCEL:
    case VK_BACK:
    case VK_TAB:
    case VK_CLEAR:
    case VK_ESCAPE:
    case VK_APPS:
    case VK_SLEEP:
    case VK_NUMLOCK:
    case VK_SCROLL:
    case VK_OEM_102:
        return true;
    }

    if (vk >= VK_SHIFT && vk <= VK_CAPITAL)
    {
        return true;
    }

    if (vk >= VK_SPACE && vk <= VK_HELP)
    {
        return true;
    }

    // Digits
    if (vk >= 0x30 && vk <= 0x39)
    {
        return true;
    }

    // Letters
    if (vk >= 0x41 && vk <= 0x5A)
    {
        return true;
    }

    if (vk >= VK_NUMPAD0 && vk <= VK_F24)
    {
        return true;
    }

    if (vk >= VK_LSHIFT && vk <= VK_LAUNCH_APP2)
    {
        return true;
    }

    if (vk >= VK_OEM_1 && vk <= VK_OEM_3)
    {
        return true;
    }

    if (vk >= VK_OEM_4 && vk <= VK_OEM_8)
    {
        return true;
    }

    if (vk >= VK_ATTN && vk <= VK_OEM_CLEAR)
    {
        return true;
    }

    return false;
}

bool only_winkey_key_held()
{
    for (int vk = VK_CANCEL; vk <= VK_OEM_CLEAR; vk++)
    {
        if (should_check(vk))
        {
            if (GetAsyncKeyState(vk) & 0x8000)
            {
                return false;
            }
        }
    }
    return true;
}