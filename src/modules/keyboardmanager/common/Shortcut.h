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

    winrt::hstring keyToHstring(ModifierKey key, const std::wstring& keyName)
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

    static DWORD decodeKey(const std::wstring& keyName)
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

public:
    // By default create an empty shortcut
    Shortcut() :
        winKey(ModifierKey::Disabled), ctrlKey(ModifierKey::Disabled), altKey(ModifierKey::Disabled), shiftKey(ModifierKey::Disabled), actionKey(NULL)
    {
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

    bool isEmpty()
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

    winrt::hstring toHstring()
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

    static Shortcut createShortcut(const winrt::hstring& input)
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
                DWORD keyCode = decodeKey(shortcutVector[i]);
                newShortcut.SetKey(keyCode);
            }
        }

        return newShortcut;
    }
};
