#include "pch.h"
#include "Shortcut.h"
#include <common/interop/keyboard_layout.h>
#include <common/interop/shared_constants.h>
#include "Helpers.h"
#include "InputInterface.h"
#include <string>
#include <sstream>

// Function to split a wstring based on a delimiter and return a vector of split strings
std::vector<std::wstring> Shortcut::splitwstring(const std::wstring& input, wchar_t delimiter)
{
    std::wstringstream ss(input);
    std::wstring item;
    std::vector<std::wstring> splittedStrings;
    while (std::getline(ss, item, delimiter))
    {
        splittedStrings.push_back(item);
    }

    return splittedStrings;
}

// Constructor to initialize Shortcut from it's virtual key code string representation.
Shortcut::Shortcut(const std::wstring& shortcutVK) :
    winKey(ModifierKey::Disabled), ctrlKey(ModifierKey::Disabled), altKey(ModifierKey::Disabled), shiftKey(ModifierKey::Disabled), actionKey(NULL)
{
    auto keys = splitwstring(shortcutVK, ';');
    SetKeyCodes(ConvertToNumbers(keys));
}

std::vector<int32_t> Shortcut::ConvertToNumbers(std::vector<std::wstring>& keys)
{
    std::vector<int32_t> keysAsNumbers;
    for (auto it : keys)
    {
        auto vkKeyCode = std::stoul(it);
        keysAsNumbers.push_back(vkKeyCode);
    }
    return keysAsNumbers;
}

// Constructor to initialize Shortcut from single key
Shortcut::Shortcut(const DWORD key)
{
    SetKey(key);
}

// Constructor to initialize Shortcut from it's virtual key code string representation.
Shortcut::Shortcut(const std::wstring& shortcutVK, const DWORD secondKeyOfChord) :
    winKey(ModifierKey::Disabled), ctrlKey(ModifierKey::Disabled), altKey(ModifierKey::Disabled), shiftKey(ModifierKey::Disabled), actionKey(NULL)
{
    auto keys = splitwstring(shortcutVK, ';');
    SetKeyCodes(ConvertToNumbers(keys));
    secondKey = secondKeyOfChord;
}

// Constructor to initialize shortcut from a list of keys
Shortcut::Shortcut(const std::vector<int32_t>& keys)
{
    SetKeyCodes(keys);
}

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
    secondKey = NULL;
    chordStarted = false;
}

// Function to return the action key
DWORD Shortcut::GetActionKey() const
{
    return actionKey;
}

bool Shortcut::IsRunProgram() const
{
    return operationType == OperationType::RunProgram;
}

bool Shortcut::IsOpenURI() const
{
    return operationType == OperationType::OpenURI;
}

DWORD Shortcut::GetSecondKey() const
{
    return secondKey;
}

bool Shortcut::HasChord() const
{
    return secondKey != NULL;
}

void Shortcut::SetChordStarted(bool started)
{
    chordStarted = started;
}

bool Shortcut::IsChordStarted() const
{
    return chordStarted;
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
bool Shortcut::CheckWinKey(const DWORD input) const
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
bool Shortcut::CheckCtrlKey(const DWORD input) const
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
bool Shortcut::CheckAltKey(const DWORD input) const
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
bool Shortcut::CheckShiftKey(const DWORD input) const
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

bool Shortcut::SetSecondKey(const DWORD input)
{
    if (secondKey == input)
    {
        return false;
    }
    secondKey = input;
    return true;
}

// Function to set a key in the shortcut based on the passed key code argument. Returns false if it is already set to the same value. This can be used to avoid UI refreshing
bool Shortcut::SetKey(const DWORD input)
{
    // Since there isn't a key for a common Win key we use the key code defined by us
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
void Shortcut::ResetKey(const DWORD input)
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

    // we always want to reset these also, I think for now since this got a little weirder when chords
    actionKey = {};
    secondKey = {};
}

// Function to return the string representation of the shortcut in virtual key codes appended in a string by ";" separator.
winrt::hstring Shortcut::ToHstringVK() const
{
    winrt::hstring output;
    if (winKey != ModifierKey::Disabled)
    {
        output = output + winrt::to_hstring(static_cast<unsigned int>(GetWinKey(ModifierKey::Both))) + winrt::to_hstring(L";");
    }
    if (ctrlKey != ModifierKey::Disabled)
    {
        output = output + winrt::to_hstring(static_cast<unsigned int>(GetCtrlKey())) + winrt::to_hstring(L";");
    }
    if (altKey != ModifierKey::Disabled)
    {
        output = output + winrt::to_hstring(static_cast<unsigned int>(GetAltKey())) + winrt::to_hstring(L";");
    }
    if (shiftKey != ModifierKey::Disabled)
    {
        output = output + winrt::to_hstring(static_cast<unsigned int>(GetShiftKey())) + winrt::to_hstring(L";");
    }
    if (actionKey != NULL)
    {
        output = output + winrt::to_hstring(static_cast<unsigned int>(GetActionKey())) + winrt::to_hstring(L";");
    }

    if (secondKey != NULL)
    {
        output = output + winrt::to_hstring(static_cast<unsigned int>(GetSecondKey())) + winrt::to_hstring(L";");
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

bool Shortcut::IsActionKey(const DWORD input)
{
    auto shortcut = Shortcut();
    shortcut.SetKey(input);
    return (shortcut.actionKey != NULL);
}

bool Shortcut::IsModifier(const DWORD input)
{
    auto shortcut = Shortcut();
    shortcut.SetKey(input);
    return (shortcut.actionKey == NULL);
}

// Function to set a shortcut from a vector of key codes
void Shortcut::SetKeyCodes(const std::vector<int32_t>& keys)
{
    Reset();

    bool foundActionKey = false;
    for (int i = 0; i < keys.size(); i++)
    {
        if (keys[i] != -1 && keys[i] != 0)
        {
            Shortcut tempShortcut = Shortcut(keys[i]);

            if (!foundActionKey && tempShortcut.actionKey != NULL)
            {
                // last key was an action key, next key is secondKey
                foundActionKey = true;
                SetKey(keys[i]);
            }
            else if (foundActionKey && tempShortcut.actionKey != NULL)
            {
                // already found actionKey, and we found another, add this as the secondKey
                secondKey = keys[i];
            }
            else
            {
                // just add whatever it is.
                SetKey(keys[i]);
            }
        }
    }
}

// Function to check if all the modifiers in the shortcut have been pressed down
bool Shortcut::CheckModifiersKeyboardState(KeyboardManagerInput::InputInterface& ii) const
{
    // Check the win key state
    if (winKey == ModifierKey::Both)
    {
        // Since VK_WIN does not exist, we check both VK_LWIN and VK_RWIN
        if ((!(ii.GetVirtualKeyState(VK_LWIN))) && (!(ii.GetVirtualKeyState(VK_RWIN))))
        {
            return false;
        }
    }
    else if (winKey == ModifierKey::Left)
    {
        if (!(ii.GetVirtualKeyState(VK_LWIN)))
        {
            return false;
        }
    }
    else if (winKey == ModifierKey::Right)
    {
        if (!(ii.GetVirtualKeyState(VK_RWIN)))
        {
            return false;
        }
    }

    // Check the ctrl key state
    if (ctrlKey == ModifierKey::Left)
    {
        if (!(ii.GetVirtualKeyState(VK_LCONTROL)))
        {
            return false;
        }
    }
    else if (ctrlKey == ModifierKey::Right)
    {
        if (!(ii.GetVirtualKeyState(VK_RCONTROL)))
        {
            return false;
        }
    }
    else if (ctrlKey == ModifierKey::Both)
    {
        if (!(ii.GetVirtualKeyState(VK_CONTROL)))
        {
            return false;
        }
    }

    // Check the alt key state
    if (altKey == ModifierKey::Left)
    {
        if (!(ii.GetVirtualKeyState(VK_LMENU)))
        {
            return false;
        }
    }
    else if (altKey == ModifierKey::Right)
    {
        if (!(ii.GetVirtualKeyState(VK_RMENU)))
        {
            return false;
        }
    }
    else if (altKey == ModifierKey::Both)
    {
        if (!(ii.GetVirtualKeyState(VK_MENU)))
        {
            return false;
        }
    }

    // Check the shift key state
    if (shiftKey == ModifierKey::Left)
    {
        if (!(ii.GetVirtualKeyState(VK_LSHIFT)))
        {
            return false;
        }
    }
    else if (shiftKey == ModifierKey::Right)
    {
        if (!(ii.GetVirtualKeyState(VK_RSHIFT)))
        {
            return false;
        }
    }
    else if (shiftKey == ModifierKey::Both)
    {
        if (!(ii.GetVirtualKeyState(VK_SHIFT)))
        {
            return false;
        }
    }

    return true;
}

// Helper method for checking if a key is in a range for cleaner code
constexpr bool in_range(DWORD key, DWORD a, DWORD b)
{
    return (key >= a && key <= b);
}

// Helper method for checking if a key is equal to a value for cleaner code
constexpr bool equals(DWORD key, DWORD a)
{
    return (key == a);
}

// Function to check if the key code is to be ignored
bool IgnoreKeyCode(DWORD key)
{
    // Ignore mouse buttons. Keeping this could cause a remapping to fail if a mouse button is also pressed at the same time
    switch (key)
    {
    case VK_LBUTTON:
    case VK_RBUTTON:
    case VK_MBUTTON:
    case VK_XBUTTON1:
    case VK_XBUTTON2:
        return true;
    }

    // As per docs: https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes
    // Undefined keys
    bool isUndefined = equals(key, 0x07) || in_range(key, 0x0E, 0x0F) || in_range(key, 0x3A, 0x40);

    // Reserved keys
    bool isReserved = in_range(key, 0x0A, 0x0B) || equals(key, 0x5E) || in_range(key, 0xB8, 0xB9) || in_range(key, 0xC1, 0xD7) || equals(key, 0xE0) || equals(key, VK_NONAME);

    // Unassigned keys
    bool isUnassigned = in_range(key, 0x88, 0x8F) || in_range(key, 0x97, 0x9F) || in_range(key, 0xD8, 0xDA) || equals(key, 0xE8);

    // OEM Specific keys. Ignore these key codes as some of them are used by IME keyboards. More information at https://github.com/microsoft/PowerToys/issues/5225
    bool isOEMSpecific = in_range(key, 0x92, 0x96) || equals(key, 0xE1) || in_range(key, 0xE3, 0xE4) || equals(key, 0xE6) || in_range(key, 0xE9, 0xF5);

    // IME keys. Ignore these key codes as some of them are used by IME keyboards. More information at https://github.com/microsoft/PowerToys/issues/6951
    bool isIME = in_range(key, VK_KANA, 0x1A) || in_range(key, VK_CONVERT, VK_MODECHANGE) || equals(key, VK_PROCESSKEY);

    if (isUndefined || isReserved || isUnassigned || isOEMSpecific || isIME)
    {
        return true;
    }
    else
    {
        return false;
    }
}

// Function to check if any keys are pressed down except those in the shortcut
bool Shortcut::IsKeyboardStateClearExceptShortcut(KeyboardManagerInput::InputInterface& ii) const
{
    // Iterate through all the virtual key codes - 0xFF is set to key down because of the Num Lock
    for (int keyVal = 1; keyVal < 0xFF; keyVal++)
    {
        // Ignore problematic key codes
        if (IgnoreKeyCode(keyVal))
        {
            continue;
        }
        // Check state of the key. If the key is pressed down but it is not part of the shortcut then the keyboard state is not clear
        if (ii.GetVirtualKeyState(keyVal))
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
            else if (keyVal != static_cast<int>(actionKey))
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
