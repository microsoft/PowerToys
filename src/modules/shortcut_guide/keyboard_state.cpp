#include "pch.h"
#include "keyboard_state.h"

bool winkey_held()
{
    auto left = GetAsyncKeyState(VK_LWIN);
    auto right = GetAsyncKeyState(VK_RWIN);
    return (left & 0x8000) || (right & 0x8000);
}

bool only_winkey_key_held()
{
    /* There are situations, when some of the keys are not registered correctly by
     GetKeyboardState. The M key can get stuck as "pressed" after Win+M, and
     Shift etc. keys are not always reported as expected.
  */
    for (int vk = 0; vk <= VK_OEM_CLEAR; ++vk)
    {
        if (vk == VK_LWIN || vk == VK_RWIN)
            continue;
        if (GetAsyncKeyState(vk) & 0x8000)
            return false;
    }
    return true;
}