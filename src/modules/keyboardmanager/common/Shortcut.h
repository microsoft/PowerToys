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

    // Function to return the name of the key with L or R prefix depending on the first argument. Second argument should be the name of the key without any prefix (ex: Win, Ctrl)
    winrt::hstring ModifierKeyNameWithSide(ModifierKey key, const std::wstring& keyName) const;

public:
    // By default create an empty shortcut
    Shortcut() :
        winKey(ModifierKey::Disabled), ctrlKey(ModifierKey::Disabled), altKey(ModifierKey::Disabled), shiftKey(ModifierKey::Disabled), actionKey(NULL)
    {
    }

    // Less than operator must be defined to use with std::map.
    inline bool operator<(const Shortcut& sc) const
    {
        // Compare win key first
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
            // If win key is equal, then compare ctrl key
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
                // If ctrl key is equal, then compare alt key
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
                    // If alt key is equal, then compare shift key
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
                        // If shift key is equal, then compare action key
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

    // Function to return the number of keys in the shortcut
    int Size() const;

    // Function to return true if the shortcut has no keys set
    bool IsEmpty() const;

    // Function to reset all the keys in the shortcut
    void Reset();

    // Function to return true if the shortcut is valid. A valid shortcut has atleast one modifier, as well as an action key
    bool IsValidShortcut() const;

    // Function to return the action key
    DWORD GetActionKey() const;

    // Function to return the virtual key code of the win key state expected in the shortcut. Argument is used to decide which win key to return in case of both. If the current shortcut doesn't use both win keys then arg is ignored. Return NULL if it is not a part of the shortcut
    DWORD GetWinKey(ModifierKey input) const;

    // Function to return the virtual key code of the ctrl key state expected in the shortcut. Return NULL if it is not a part of the shortcut
    DWORD GetCtrlKey() const;

    // Function to return the virtual key code of the alt key state expected in the shortcut. Return NULL if it is not a part of the shortcut
    DWORD GetAltKey() const;

    // Function to return the virtual key code of the shift key state expected in the shortcut. Return NULL if it is not a part of the shortcut
    DWORD GetShiftKey() const;

    // Function to check if the input key matches the win key expected in the shortcut
    bool CheckWinKey(DWORD input) const;

    // Function to check if the input key matches the ctrl key expected in the shortcut
    bool CheckCtrlKey(DWORD input) const;

    // Function to check if the input key matches the alt key expected in the shortcut
    bool CheckAltKey(DWORD input) const;

    // Function to check if the input key matches the shift key expected in the shortcut
    bool CheckShiftKey(DWORD input) const;

    // Function to set a key in the shortcut based on the passed key code argument. Since there is no VK_WIN code, use the second argument for setting common win key. If isWinBoth is true then first arg is ignored. Returns false if it is already set to the same value. This can be used to avoid UI refreshing
    bool SetKey(DWORD input, bool isWinBoth = false);

    // Function to reset the state of a shortcut key based on the passed key code argument. Since there is no VK_WIN code, use the second argument for setting common win key.
    void ResetKey(DWORD input, bool isWinBoth = false);

    // Function to return the string representation of the shortcut
    winrt::hstring ToHstring() const;

    // Function to check if all the modifiers in the shortcut have been pressed down
    bool CheckModifiersKeyboardState() const;

    // Function to check if any keys are pressed down except those in the shortcut
    bool IsKeyboardStateClearExceptShortcut() const;

    // Function to get the number of modifiers that are common between the current shortcut and the shortcut in the argument
    int GetCommonModifiersCount(const Shortcut& input) const;

    // Function to return the virtual key code from the name of the key
    static DWORD DecodeKey(const std::wstring& keyName);

    // Function to create a shortcut object from its string representation
    static Shortcut CreateShortcut(const winrt::hstring& input);
};
