#pragma once
#include "Shortcut.h"

class LayoutMap;

namespace Helpers
{
    // Type to distinguish between keys
    enum class KeyType
    {
        Win,
        Ctrl,
        Alt,
        Shift,
        Action
    };

    // Function to check if the key is a modifier key
    bool IsModifierKey(DWORD key);

    // Function to get the type of the key
    KeyType GetKeyType(DWORD key);

    // Function to set the value of a key event based on the arguments
    void SetKeyEvent(LPINPUT keyEventArray, int index, DWORD inputType, WORD keyCode, DWORD flags, ULONG_PTR extraInfo);

    // Function to set the dummy key events used for remapping shortcuts, required to ensure releasing a modifier doesn't trigger another action (For example, Win->Start Menu or Alt->Menu bar)
    void SetDummyKeyEvent(LPINPUT keyEventArray, int& index, ULONG_PTR extraInfo);

    // Function to return window handle for a full screen UWP app
    HWND GetFullscreenUWPWindowHandle();

    // Function to return the executable name of the application in focus
    std::wstring GetCurrentApplication(bool keepPath);

    // Function to set key events for modifier keys: When shortcutToCompare is passed (non-empty shortcut), then the key event is sent only if both shortcut's don't have the same modifier key. When keyToBeReleased is passed (non-NULL), then the key event is sent if either the shortcuts don't have the same modifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
    void SetModifierKeyEvents(const Shortcut& shortcutToBeSent, const ModifierKey& winKeyInvoked, LPINPUT keyEventArray, int& index, bool isKeyDown, ULONG_PTR extraInfoFlag, const Shortcut& shortcutToCompare = Shortcut(), const DWORD& keyToBeReleased = NULL);

    // Function to filter the key codes for artificial key codes
    int32_t FilterArtificialKeys(const int32_t& key);

    // Function to sort a vector of shortcuts based on it's size
    void SortShortcutVectorBasedOnSize(std::vector<Shortcut>& shortcutVector);
}