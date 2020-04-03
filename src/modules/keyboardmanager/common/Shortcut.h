#pragma once
#include "Helpers.h"
#include <interface/lowlevel_keyboard_event_data.h>

// Enum type to store different states of the win key
enum class ModifierKey
{
    Disabled,
    Left,
    Right,
    Both
};

class Shortcut
{
private:
    ModifierKey winKey;
    ModifierKey ctrlKey;
    ModifierKey altKey;
    ModifierKey shiftKey;
    DWORD actionKey;

    winrt::hstring keyToHstring(ModifierKey key, const std::wstring& keyName) const
    {
        if (key == ModifierKey::Left)
        {
            return winrt::to_hstring(L"L") + winrt::to_hstring(keyName.c_str());
        }
        else if (key == ModifierKey::Right)
        {
            return winrt::to_hstring(L"R") + winrt::to_hstring(keyName.c_str());
        }
        else if (key == ModifierKey::Both)
        {
            return winrt::to_hstring(keyName.c_str());
        }

        return winrt::hstring();
    }

public:
    static DWORD DecodeKey(const std::wstring& keyName)
    {
        if (keyName == L"LWin")
        {
            return VK_LWIN;
        }
        else if (keyName == L"RWin")
        {
            return VK_RWIN;
        }
        else if (keyName == L"LCtrl")
        {
            return VK_LCONTROL;
        }
        else if (keyName == L"RCtrl")
        {
            return VK_RCONTROL;
        }
        else if (keyName == L"Ctrl")
        {
            return VK_CONTROL;
        }
        else if (keyName == L"LAlt")
        {
            return VK_LMENU;
        }
        else if (keyName == L"RAlt")
        {
            return VK_RMENU;
        }
        else if (keyName == L"Alt")
        {
            return VK_MENU;
        }
        else if (keyName == L"LShift")
        {
            return VK_LSHIFT;
        }
        else if (keyName == L"RShift")
        {
            return VK_RSHIFT;
        }
        else if (keyName == L"Shift")
        {
            return VK_SHIFT;
        }
        else
        {
            return std::stoi(keyName);
        }
    }

    // By default create an empty shortcut
    Shortcut() :
        winKey(ModifierKey::Disabled), ctrlKey(ModifierKey::Disabled), altKey(ModifierKey::Disabled), shiftKey(ModifierKey::Disabled), actionKey(NULL)
    {
    }

    // Less than operator must be defined to use with std::map
    inline bool operator<(const Shortcut& sc) const
    {
        if (winKey < sc.winKey)
        {
            return true;
        }
        else if (winKey > sc.winKey)
        {
            return false;
        }
        else
        {
            if (ctrlKey < sc.ctrlKey)
            {
                return true;
            }
            else if (ctrlKey > sc.ctrlKey)
            {
                return false;
            }
            else
            {
                if (altKey < sc.altKey)
                {
                    return true;
                }
                else if (altKey > sc.altKey)
                {
                    return false;
                }
                else
                {
                    if (shiftKey < sc.shiftKey)
                    {
                        return true;
                    }
                    else if (shiftKey > sc.shiftKey)
                    {
                        return false;
                    }
                    else
                    {
                        if (actionKey < sc.actionKey)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
        }
    }

    DWORD GetActionKey() const
    {
        return actionKey;
    }

    int Size() const
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

    // Return false if it is already set to the same value. This can be used to avoid UI refreshing
    bool SetKey(DWORD input, bool isWinBoth = false)
    {
        // Since there isn't a key for a common Win key this is handled with a separate argument
        if (isWinBoth)
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

    void ResetKey(DWORD input, bool isWinBoth = false)
    {
        // Since there isn't a key for a common Win key this is handled with a separate argument
        if (isWinBoth || input == VK_LWIN || input == VK_RWIN)
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

    bool IsEmpty() const
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

    void Reset()
    {
        winKey = ModifierKey::Disabled;
        ctrlKey = ModifierKey::Disabled;
        altKey = ModifierKey::Disabled;
        shiftKey = ModifierKey::Disabled;
        actionKey = NULL;
    }

    winrt::hstring ToHstring() const
    {
        winrt::hstring output;
        if (winKey != ModifierKey::Disabled)
        {
            output = output + keyToHstring(winKey, L"Win") + winrt::to_hstring(L" ");
        }
        if (ctrlKey != ModifierKey::Disabled)
        {
            output = output + keyToHstring(ctrlKey, L"Ctrl") + winrt::to_hstring(L" ");
        }
        if (altKey != ModifierKey::Disabled)
        {
            output = output + keyToHstring(altKey, L"Alt") + winrt::to_hstring(L" ");
        }
        if (shiftKey != ModifierKey::Disabled)
        {
            output = output + keyToHstring(shiftKey, L"Shift") + winrt::to_hstring(L" ");
        }
        if (actionKey != NULL)
        {
            output = output + winrt::to_hstring((unsigned int)actionKey);
        }
        return output;
    }

    static Shortcut CreateShortcut(const winrt::hstring& input)
    {
        Shortcut newShortcut;
        std::wstring shortcutWstring = input.c_str();
        std::vector<std::wstring> shortcutVector = splitwstring(shortcutWstring, L' ');
        for (int i = 0; i < shortcutVector.size(); i++)
        {
            if (shortcutVector[i] == L"Win")
            {
                newShortcut.SetKey(NULL, true);
            }
            else
            {
                DWORD keyCode = DecodeKey(shortcutVector[i]);
                newShortcut.SetKey(keyCode);
            }
        }

        return newShortcut;
    }

    bool IsValidShortcut() const
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

    DWORD GetWinKey() const
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
            return VK_LWIN;
        }
    }

    DWORD GetCtrlKey() const
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

    DWORD GetAltKey() const
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

    DWORD GetShiftKey() const
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

    // Function to check if the modifiers in the shortcut have been pressed down
    bool CheckModifiersKeyboardState() const
    {
        // Check all the modifier keys
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

    // Function to check if any keys are pressed down except those passed in the argument
    bool IsKeyboardStateClearExceptShortcut() const
    {
        for (int keyVal = 0; keyVal < 0x100; keyVal++)
        {
            // Skip mouse buttons. Keeping this could cause a remapping to fail if a mouse button is also pressed at the same time
            if (keyVal == VK_LBUTTON || keyVal == VK_RBUTTON || keyVal == VK_MBUTTON || keyVal == VK_XBUTTON1 || keyVal == VK_XBUTTON2)
            {
                continue;
            }
            // Check state of the key
            if (GetAsyncKeyState(keyVal) & 0x8000)
            {
                // If the key is not part of the shortcut then the keyboard state is not clear
                // If win key is pressed but isn't part of the shortcut
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
                // If ctrl key is pressed but isn't part of the shortcut
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
                // If alt key is pressed but isn't part of the shortcut
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
                // If shift key is pressed but isn't part of the shortcut
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
                // If any other key is pressed but it doesn't match the action key
                else if (keyVal != actionKey)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Function to check if all the modifiers in the first shorcut are present in the second shortcut, i.e. Modifiers(src) are a subset of Modifiers(dest)
    int GetCommonModifiersCount(const Shortcut& input) const
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
};
