#pragma once
#include "Shortcut.h"

namespace winrt
{
    struct hstring;
    namespace Windows::Foundation
    {
        struct IInspectable;
        namespace Collections
        {
            template<typename T>
            struct IVector;
        }
    }
}

class LayoutMap;

namespace KeyboardManagerHelper
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

    // Type to store codes for different errors
    enum class ErrorType
    {
        NoError,
        SameKeyPreviouslyMapped,
        MapToSameKey,
        ConflictingModifierKey,
        SameShortcutPreviouslyMapped,
        MapToSameShortcut,
        ConflictingModifierShortcut,
        WinL,
        CtrlAltDel,
        RemapUnsuccessful,
        SaveFailed,
        ShortcutStartWithModifier,
        ShortcutCannotHaveRepeatedModifier,
        ShortcutAtleast2Keys,
        ShortcutOneActionKey,
        ShortcutNotMoreThanOneActionKey,
        ShortcutMaxShortcutSizeOneActionKey,
        ShortcutDisableAsActionKey
    };

    // Enum type to store possible decision for input in the low level hook
    enum class KeyboardHookDecision
    {
        ContinueExec,
        Suppress,
        SkipHook
    };

    // Function to split a wstring based on a delimiter and return a vector of split strings
    std::vector<std::wstring> splitwstring(const std::wstring& input, wchar_t delimiter);

    // Function to return the next sibling element for an element under a stack panel
    winrt::Windows::Foundation::IInspectable getSiblingElement(winrt::Windows::Foundation::IInspectable const& element);

    // Function to return if the key is an extended key which requires the use of the extended key flag
    bool IsExtendedKey(DWORD key);

    // Function to check if the key is a modifier key
    bool IsModifierKey(DWORD key);

    // Function to get the type of the key
    KeyType GetKeyType(DWORD key);

    // Function to check if two keys are equal or cover the same set of keys. Return value depends on type of overlap
    ErrorType DoKeysOverlap(DWORD first, DWORD second);

    // Function to return the error message
    winrt::hstring GetErrorMessage(ErrorType errorType);

    // Function to return the list of key name in the order for the drop down based on the key codes
    winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::IInspectable> ToBoxValue(const std::vector<std::pair<DWORD,std::wstring>>& list);

    // Function to set the value of a key event based on the arguments
    void SetKeyEvent(LPINPUT keyEventArray, int index, DWORD inputType, WORD keyCode, DWORD flags, ULONG_PTR extraInfo);

    // Function to set the dummy key events used for remapping shortcuts, required to ensure releasing a modifier doesn't trigger another action (For example, Win->Start Menu or Alt->Menu bar)
    void SetDummyKeyEvent(LPINPUT keyEventArray, int& index, ULONG_PTR extraInfo);

    // Function to return window handle for a full screen UWP app
    HWND GetFullscreenUWPWindowHandle();

    // Function to return the executable name of the application in focus
    std::wstring GetCurrentApplication(bool keepPath);

    // Function to set key events for modifier keys: When shortcutToCompare is passed (non-empty shortcut), then the key event is sent only if both shortcut's don't have the same modifier key. When keyToBeReleased is passed (non-NULL), then the key event is sent if either the shortcuts don't have the same modfifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
    void SetModifierKeyEvents(const Shortcut& shortcutToBeSent, const ModifierKey& winKeyInvoked, LPINPUT keyEventArray, int& index, bool isKeyDown, ULONG_PTR extraInfoFlag, const Shortcut& shortcutToCompare = Shortcut(), const DWORD& keyToBeReleased = NULL);

    // Function to filter the key codes for artificial key codes
    int32_t FilterArtificialKeys(const int32_t& key);

    // Function to sort a vector of shortcuts based on it's size
    void SortShortcutVectorBasedOnSize(std::vector<Shortcut>& shortcutVector);

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(const std::vector<int32_t>& currentKeys, int selectedKeyCodes);
}