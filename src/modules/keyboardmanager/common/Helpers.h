#pragma once
#include <vector>
#include <winrt/Windows.System.h>
#include <winrt/Windows.Foundation.h>

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
        MissingKey,
        ShortcutStartWithModifier,
        ShortcutCannotHaveRepeatedModifier,
        ShortcutAtleast2Keys,
        ShortcutOneActionKey,
        ShortcutNotMoreThanOneActionKey,
        ShortcutMaxShortcutSizeOneActionKey
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
    winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::IInspectable> ToBoxValue(const std::vector<std::wstring>& list);

    // Function to set the value of a key event based on the arguments
    void SetKeyEvent(LPINPUT keyEventArray, int index, DWORD inputType, WORD keyCode, DWORD flags, ULONG_PTR extraInfo);

    // Function to return the window in focus
    HWND GetFocusWindowHandle();

    // Function to return the executable name of the application in focus
    std::wstring GetCurrentApplication(bool keepPath);
}