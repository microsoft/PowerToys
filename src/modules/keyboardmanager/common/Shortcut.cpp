#include "pch.h"
#include "Shortcut.h"

// Function to return the number of keys in the shortcut
int Shortcut::Size() const
{
    int size = 0;
    if (winKey != ModifierKey::Disabled)
    {
        size += 1;
    }
    if (ctrlKey != ModifierKey::Disabled)
    {
        size += 1;
    }
    if (altKey != ModifierKey::Disabled)
    {
        size += 1;
    }
    if (shiftKey != ModifierKey::Disabled)
    {
        size += 1;
    }
    if (actionKey != NULL)
    {
        size += 1;
    }

    return size;
}

// Function to return true if the shortcut has no keys set
bool Shortcut::IsEmpty() const
{
    if (winKey == ModifierKey::Disabled && ctrlKey == ModifierKey::Disabled && altKey == ModifierKey::Disabled && shiftKey == ModifierKey::Disabled && actionKey == NULL)
    {
        return true;
    }
    else
    {
        return false;
    }
}

// Function to reset all the keys in the shortcut
void Shortcut::Reset()
{
    winKey = ModifierKey::Disabled;
    ctrlKey = ModifierKey::Disabled;
    altKey = ModifierKey::Disabled;
    shiftKey = ModifierKey::Disabled;
    actionKey = NULL;
}

// Function to return true if the shortcut is valid. A valid shortcut has atleast one modifier, as well as an action key
bool Shortcut::IsValidShortcut() const
{
    if (actionKey != NULL)
    {
        if (winKey != ModifierKey::Disabled || ctrlKey != ModifierKey::Disabled || altKey != ModifierKey::Disabled || shiftKey != ModifierKey::Disabled)
        {
            return true;
        }
    }

    return false;
}

// Function to return the action key
DWORD Shortcut::GetActionKey() const
{
    return actionKey;
}

// Function to return the virtual key code of the win key state expected in the shortcut. Argument is used to decide which win key to return in case of both. If the current shortcut doesn't use both win keys then arg is ignored. Return NULL if it is not a part of the shortcut
DWORD Shortcut::GetWinKey(const ModifierKey& input) const
{
    if (winKey == ModifierKey::Disabled)
    {
        return NULL;
    }
    else if (winKey == ModifierKey::Left)
    {
        return VK_LWIN;
    }
    else if (winKey == ModifierKey::Right)
    {
        return VK_RWIN;
    }
    else
    {
        // Since VK_WIN does not exist if right windows key is to be sent based on the argument, then return VK_RWIN as the win key (since that will be used to release it).
        if (input == ModifierKey::Right)
        {
            return VK_RWIN;
        }
        else if (input == ModifierKey::Left || input == ModifierKey::Disabled)
        {
            //return VK_LWIN by default
            return VK_LWIN;
        }
        else
        {
            return CommonSharedConstants::VK_WIN_BOTH;
        }
    }
}

// Function to return the virtual key code of the ctrl key state expected in the shortcut. Return NULL if it is not a part of the shortcut
DWORD Shortcut::GetCtrlKey() const
{
    if (ctrlKey == ModifierKey::Disabled)
    {
        return NULL;
    }
    else if (ctrlKey == ModifierKey::Left)
    {
        return VK_LCONTROL;
    }
    else if (ctrlKey == ModifierKey::Right)
    {
        return VK_RCONTROL;
    }
    else
    {
        return VK_CONTROL;
    }
}

// Function to return the virtual key code of the alt key state expected in the shortcut. Return NULL if it is not a part of the shortcut
DWORD Shortcut::GetAltKey() const
{
    if (altKey == ModifierKey::Disabled)
    {
        return NULL;
    }
    else if (altKey == ModifierKey::Left)
    {
        return VK_LMENU;
    }
    else if (altKey == ModifierKey::Right)
    {
        return VK_RMENU;
    }
    else
    {
        return VK_MENU;
    }
}

// Function to return the virtual key code of the shift key state expected in the shortcut. Return NULL if it is not a part of the shortcut
DWORD Shortcut::GetShiftKey() const
{
    if (shiftKey == ModifierKey::Disabled)
    {
        return NULL;
    }
    else if (shiftKey == ModifierKey::Left)
    {
        return VK_LSHIFT;
    }
    else if (shiftKey == ModifierKey::Right)
    {
        return VK_RSHIFT;
    }
    else
    {
        return VK_SHIFT;
    }
}

// Function to check if the input key matches the win key expected in the shortcut
bool Shortcut::CheckWinKey(const DWORD& input) const
{
    if (winKey == ModifierKey::Disabled)
    {
        return false;
    }
    else if (winKey == ModifierKey::Left)
    {
        return (VK_LWIN == input);
    }
    else if (winKey == ModifierKey::Right)
    {
        return (VK_RWIN == input);
    }
    // If ModifierKey::Both then return true if either left or right (VK_WIN does not exist)
    else
    {
        return (VK_LWIN == input) || (VK_RWIN == input);
    }
}

// Function to check if the input key matches the ctrl key expected in the shortcut
bool Shortcut::CheckCtrlKey(const DWORD& input) const
{
    if (ctrlKey == ModifierKey::Disabled)
    {
        return false;
    }
    else if (ctrlKey == ModifierKey::Left)
    {
        return (VK_LCONTROL == input);
    }
    else if (ctrlKey == ModifierKey::Right)
    {
        return (VK_RCONTROL == input);
    }
    // If ModifierKey::Both then return true if either left or right or common
    else
    {
        return (VK_CONTROL == input) || (VK_LCONTROL == input) || (VK_RCONTROL == input);
    }
}

// Function to check if the input key matches the alt key expected in the shortcut
bool Shortcut::CheckAltKey(const DWORD& input) const
{
    if (altKey == ModifierKey::Disabled)
    {
        return false;
    }
    else if (altKey == ModifierKey::Left)
    {
        return (VK_LMENU == input);
    }
    else if (altKey == ModifierKey::Right)
    {
        return (VK_RMENU == input);
    }
    // If ModifierKey::Both then return true if either left or right or common
    else
    {
        return (VK_MENU == input) || (VK_LMENU == input) || (VK_RMENU == input);
    }
}

// Function to check if the input key matches the shift key expected in the shortcut
bool Shortcut::CheckShiftKey(const DWORD& input) const
{
    if (shiftKey == ModifierKey::Disabled)
    {
        return false;
    }
    else if (shiftKey == ModifierKey::Left)
    {
        return (VK_LSHIFT == input);
    }
    else if (shiftKey == ModifierKey::Right)
    {
        return (VK_RSHIFT == input);
    }
    // If ModifierKey::Both then return true if either left or right or common
    else
    {
        return (VK_SHIFT == input) || (VK_LSHIFT == input) || (VK_RSHIFT == input);
    }
}

// Function to set a key in the shortcut based on the passed key code argument. Returns false if it is already set to the same value. This can be used to avoid UI refreshing
bool Shortcut::SetKey(const DWORD& input)
{
    // Since there isn't a key for a common Win key this is handled with a separate argument
    if (input == CommonSharedConstants::VK_WIN_BOTH)
    {
        if (winKey == ModifierKey::Both)
        {
            return false;
        }
        winKey = ModifierKey::Both;
    }
    else if (input == VK_LWIN)
    {
        if (winKey == ModifierKey::Left)
        {
            return false;
        }
        winKey = ModifierKey::Left;
    }
    else if (input == VK_RWIN)
    {
        if (winKey == ModifierKey::Right)
        {
            return false;
        }
        winKey = ModifierKey::Right;
    }
    else if (input == VK_LCONTROL)
    {
        if (ctrlKey == ModifierKey::Left)
        {
            return false;
        }
        ctrlKey = ModifierKey::Left;
    }
    else if (input == VK_RCONTROL)
    {
        if (ctrlKey == ModifierKey::Right)
        {
            return false;
        }
        ctrlKey = ModifierKey::Right;
    }
    else if (input == VK_CONTROL)
    {
        if (ctrlKey == ModifierKey::Both)
        {
            return false;
        }
        ctrlKey = ModifierKey::Both;
    }
    else if (input == VK_LMENU)
    {
        if (altKey == ModifierKey::Left)
        {
            return false;
        }
        altKey = ModifierKey::Left;
    }
    else if (input == VK_RMENU)
    {
        if (altKey == ModifierKey::Right)
        {
            return false;
        }
        altKey = ModifierKey::Right;
    }
    else if (input == VK_MENU)
    {
        if (altKey == ModifierKey::Both)
        {
            return false;
        }
        altKey = ModifierKey::Both;
    }
    else if (input == VK_LSHIFT)
    {
        if (shiftKey == ModifierKey::Left)
        {
            return false;
        }
        shiftKey = ModifierKey::Left;
    }
    else if (input == VK_RSHIFT)
    {
        if (shiftKey == ModifierKey::Right)
        {
            return false;
        }
        shiftKey = ModifierKey::Right;
    }
    else if (input == VK_SHIFT)
    {
        if (shiftKey == ModifierKey::Both)
        {
            return false;
        }
        shiftKey = ModifierKey::Both;
    }
    else
    {
        if (actionKey == input)
        {
            return false;
        }
        actionKey = input;
    }

    return true;
}

// Function to reset the state of a shortcut key based on the passed key code argument. Since there is no VK_WIN code, use the second argument for setting common win key.
void Shortcut::ResetKey(const DWORD& input)
{
    // Since there isn't a key for a common Win key this is handled with a separate argument.
    if (input == CommonSharedConstants::VK_WIN_BOTH || input == VK_LWIN || input == VK_RWIN)
    {
        winKey = ModifierKey::Disabled;
    }
    else if (input == VK_LCONTROL || input == VK_RCONTROL || input == VK_CONTROL)
    {
        ctrlKey = ModifierKey::Disabled;
    }
    else if (input == VK_LMENU || input == VK_RMENU || input == VK_MENU)
    {
        altKey = ModifierKey::Disabled;
    }
    else if (input == VK_LSHIFT || input == VK_RSHIFT || input == VK_SHIFT)
    {
        shiftKey = ModifierKey::Disabled;
    }
    else
    {
        actionKey = NULL;
    }
}

// Function to return a vector of hstring for each key in the display order
std::vector<winrt::hstring> Shortcut::GetKeyVector(LayoutMap& keyboardMap) const
{
    std::vector<winrt::hstring> keys;
    if (winKey != ModifierKey::Disabled)
    {
        keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(GetWinKey(ModifierKey::Both)).c_str()));
    }
    if (ctrlKey != ModifierKey::Disabled)
    {
        keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(GetCtrlKey()).c_str()));
    }
    if (altKey != ModifierKey::Disabled)
    {
        keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(GetAltKey()).c_str()));
    }
    if (shiftKey != ModifierKey::Disabled)
    {
        keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(GetShiftKey()).c_str()));
    }
    if (actionKey != NULL)
    {
        keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(actionKey).c_str()));
    }
    return keys;
}

// Function to return the string representation of the shortcut in virtual key codes appended in a string by ";" separator.
winrt::hstring Shortcut::ToHstringVK() const
{
    winrt::hstring output;
    if (winKey != ModifierKey::Disabled)
    {
        output = output + winrt::to_hstring((unsigned int)GetWinKey(ModifierKey::Both)) + winrt::to_hstring(L";");
    }
    if (ctrlKey != ModifierKey::Disabled)
    {
        output = output + winrt::to_hstring((unsigned int)GetCtrlKey()) + winrt::to_hstring(L";");
    }
    if (altKey != ModifierKey::Disabled)
    {
        output = output + winrt::to_hstring((unsigned int)GetAltKey()) + winrt::to_hstring(L";");
    }
    if (shiftKey != ModifierKey::Disabled)
    {
        output = output + winrt::to_hstring((unsigned int)GetShiftKey()) + winrt::to_hstring(L";");
    }
    if (actionKey != NULL)
    {
        output = output + winrt::to_hstring((unsigned int)GetActionKey()) + winrt::to_hstring(L";");
    }

    if (!output.empty())
    {
        output = winrt::hstring(output.c_str(), output.size() - 1);
    }

    return output;
}

// Function to return a vector of key codes in the display order
std::vector<DWORD> Shortcut::GetKeyCodes()
{
    std::vector<DWORD> keys;
    if (winKey != ModifierKey::Disabled)
    {
        keys.push_back(GetWinKey(ModifierKey::Both));
    }
    if (ctrlKey != ModifierKey::Disabled)
    {
        keys.push_back(GetCtrlKey());
    }
    if (altKey != ModifierKey::Disabled)
    {
        keys.push_back(GetAltKey());
    }
    if (shiftKey != ModifierKey::Disabled)
    {
        keys.push_back(GetShiftKey());
    }
    if (actionKey != NULL)
    {
        keys.push_back(actionKey);
    }
    return keys;
}

// Function to set a shortcut from a vector of key codes
void Shortcut::SetKeyCodes(const std::vector<DWORD>& keys)
{
    Reset();
    for (int i = 0; i < keys.size(); i++)
    {
        SetKey(keys[i]);
    }
}

// Function to check if all the modifiers in the shortcut have been pressed down
bool Shortcut::CheckModifiersKeyboardState() const
{
    // Check the win key state
    if (winKey == ModifierKey::Both)
    {
        // Since VK_WIN does not exist, we check both VK_LWIN and VK_RWIN
        if ((!(GetAsyncKeyState(VK_LWIN) & 0x8000)) && (!(GetAsyncKeyState(VK_RWIN) & 0x8000)))
        {
            return false;
        }
    }
    else if (winKey == ModifierKey::Left)
    {
        if (!(GetAsyncKeyState(VK_LWIN) & 0x8000))
        {
            return false;
        }
    }
    else if (winKey == ModifierKey::Right)
    {
        if (!(GetAsyncKeyState(VK_RWIN) & 0x8000))
        {
            return false;
        }
    }

    // Check the ctrl key state
    if (ctrlKey == ModifierKey::Left)
    {
        if (!(GetAsyncKeyState(VK_LCONTROL) & 0x8000))
        {
            return false;
        }
    }
    else if (ctrlKey == ModifierKey::Right)
    {
        if (!(GetAsyncKeyState(VK_RCONTROL) & 0x8000))
        {
            return false;
        }
    }
    else if (ctrlKey == ModifierKey::Both)
    {
        if (!(GetAsyncKeyState(VK_CONTROL) & 0x8000))
        {
            return false;
        }
    }

    // Check the alt key state
    if (altKey == ModifierKey::Left)
    {
        if (!(GetAsyncKeyState(VK_LMENU) & 0x8000))
        {
            return false;
        }
    }
    else if (altKey == ModifierKey::Right)
    {
        if (!(GetAsyncKeyState(VK_RMENU) & 0x8000))
        {
            return false;
        }
    }
    else if (altKey == ModifierKey::Both)
    {
        if (!(GetAsyncKeyState(VK_MENU) & 0x8000))
        {
            return false;
        }
    }

    // Check the shift key state
    if (shiftKey == ModifierKey::Left)
    {
        if (!(GetAsyncKeyState(VK_LSHIFT) & 0x8000))
        {
            return false;
        }
    }
    else if (shiftKey == ModifierKey::Right)
    {
        if (!(GetAsyncKeyState(VK_RSHIFT) & 0x8000))
        {
            return false;
        }
    }
    else if (shiftKey == ModifierKey::Both)
    {
        if (!(GetAsyncKeyState(VK_SHIFT) & 0x8000))
        {
            return false;
        }
    }

    return true;
}

// Function to check if any keys are pressed down except those in the shortcut
bool Shortcut::IsKeyboardStateClearExceptShortcut() const
{
    // Iterate through all the virtual key codes - 0xFF is set to key down because of the Num Lock
    for (int keyVal = 1; keyVal < 0xFF; keyVal++)
    {
        // Skip mouse buttons. Keeping this could cause a remapping to fail if a mouse button is also pressed at the same time
        if (keyVal == VK_LBUTTON || keyVal == VK_RBUTTON || keyVal == VK_MBUTTON || keyVal == VK_XBUTTON1 || keyVal == VK_XBUTTON2)
        {
            continue;
        }
        // Check state of the key. If the key is pressed down but it is not part of the shortcut then the keyboard state is not clear
        if (GetAsyncKeyState(keyVal) & 0x8000)
        {
            // If one of the win keys is pressed check if it is part of the shortcut
            if (keyVal == VK_LWIN)
            {
                if (winKey != ModifierKey::Left && winKey != ModifierKey::Both)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            else if (keyVal == VK_RWIN)
            {
                if (winKey != ModifierKey::Right && winKey != ModifierKey::Both)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            // If one of the ctrl keys is pressed check if it is part of the shortcut
            else if (keyVal == VK_LCONTROL)
            {
                if (ctrlKey != ModifierKey::Left && ctrlKey != ModifierKey::Both)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            else if (keyVal == VK_RCONTROL)
            {
                if (ctrlKey != ModifierKey::Right && ctrlKey != ModifierKey::Both)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            else if (keyVal == VK_CONTROL)
            {
                if (ctrlKey == ModifierKey::Disabled)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            // If one of the alt keys is pressed check if it is part of the shortcut
            else if (keyVal == VK_LMENU)
            {
                if (altKey != ModifierKey::Left && altKey != ModifierKey::Both)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            else if (keyVal == VK_RMENU)
            {
                if (altKey != ModifierKey::Right && altKey != ModifierKey::Both)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            else if (keyVal == VK_MENU)
            {
                if (altKey == ModifierKey::Disabled)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            // If one of the shift keys is pressed check if it is part of the shortcut
            else if (keyVal == VK_LSHIFT)
            {
                if (shiftKey != ModifierKey::Left && shiftKey != ModifierKey::Both)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            else if (keyVal == VK_RSHIFT)
            {
                if (shiftKey != ModifierKey::Right && shiftKey != ModifierKey::Both)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            else if (keyVal == VK_SHIFT)
            {
                if (shiftKey == ModifierKey::Disabled)
                {
                    return false;
                }
                else
                {
                    continue;
                }
            }
            // If any other key is pressed check if it is the action key
            else if (keyVal != actionKey)
            {
                return false;
            }
        }
    }

    return true;
}

// Function to get the number of modifiers that are common between the current shortcut and the shortcut in the argument
int Shortcut::GetCommonModifiersCount(const Shortcut& input) const
{
    int commonElements = 0;
    if ((winKey == input.winKey) && winKey != ModifierKey::Disabled)
    {
        commonElements += 1;
    }
    if ((ctrlKey == input.ctrlKey) && ctrlKey != ModifierKey::Disabled)
    {
        commonElements += 1;
    }
    if ((altKey == input.altKey) && altKey != ModifierKey::Disabled)
    {
        commonElements += 1;
    }
    if ((shiftKey == input.shiftKey) && shiftKey != ModifierKey::Disabled)
    {
        commonElements += 1;
    }

    return commonElements;
}

// Function to check if the two shortcuts are equal or cover the same set of keys. Return value depends on type of overlap
KeyboardManagerHelper::ErrorType Shortcut::DoKeysOverlap(const Shortcut& first, const Shortcut& second)
{
    if (first.IsValidShortcut() && second.IsValidShortcut())
    {
        // If the shortcuts are equal
        if (first == second)
        {
            return KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped;
        }
        // action keys match
        else if (first.actionKey == second.actionKey)
        {
            // corresponding modifiers are either both disabled or both not disabled - this ensures that both match in types of modifiers i.e. Ctrl(l/r/c) Shift (l/r/c) A matches Ctrl(l/r/c) Shift (l/r/c) A
            if (((first.winKey != ModifierKey::Disabled && second.winKey != ModifierKey::Disabled) || (first.winKey == ModifierKey::Disabled && second.winKey == ModifierKey::Disabled)) && 
                ((first.ctrlKey != ModifierKey::Disabled && second.ctrlKey != ModifierKey::Disabled) || (first.ctrlKey == ModifierKey::Disabled && second.ctrlKey == ModifierKey::Disabled)) && 
                ((first.altKey != ModifierKey::Disabled && second.altKey != ModifierKey::Disabled) || (first.altKey == ModifierKey::Disabled && second.altKey == ModifierKey::Disabled)) && 
                ((first.shiftKey != ModifierKey::Disabled && second.shiftKey != ModifierKey::Disabled) || (first.shiftKey == ModifierKey::Disabled && second.shiftKey == ModifierKey::Disabled)))
            {
                // If one of the modifier is common
                if ((first.winKey == ModifierKey::Both || second.winKey == ModifierKey::Both) || 
                    (first.ctrlKey == ModifierKey::Both || second.ctrlKey == ModifierKey::Both) || 
                    (first.altKey == ModifierKey::Both || second.altKey == ModifierKey::Both) || 
                    (first.shiftKey == ModifierKey::Both || second.shiftKey == ModifierKey::Both))
                {
                    return KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut;
                }
            }
        }
    }

    return KeyboardManagerHelper::ErrorType::NoError;
}

// Function to check if the shortcut is illegal (i.e. Win+L or Ctrl+Alt+Del)
KeyboardManagerHelper::ErrorType Shortcut::IsShortcutIllegal() const
{
    // Win+L
    if (winKey != ModifierKey::Disabled && ctrlKey == ModifierKey::Disabled && altKey == ModifierKey::Disabled && shiftKey == ModifierKey::Disabled && actionKey == 0x4C)
    {
        return KeyboardManagerHelper::ErrorType::WinL;
    }

    // Ctrl+Alt+Del
    if (winKey == ModifierKey::Disabled && ctrlKey != ModifierKey::Disabled && altKey != ModifierKey::Disabled && shiftKey == ModifierKey::Disabled && actionKey == VK_DELETE)
    {
        return KeyboardManagerHelper::ErrorType::CtrlAltDel;
    }

    return KeyboardManagerHelper::ErrorType::NoError;
}
