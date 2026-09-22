#pragma once
#include <cstddef>
#include <optional>

#include <common/hooks/LowlevelKeyboardEvent.h>

#include "Shortcut.h"
#include "RemapShortcut.h"

class LayoutMap;

namespace KeyboardManagerInput
{
    class InputInterface;
}

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

    // Functions to encode that a key is originated from numpad
    DWORD EncodeKeyNumpadOrigin(const DWORD key, const bool extended);
    DWORD ClearKeyNumpadOrigin(const DWORD key);
    bool IsNumpadOriginated(const DWORD key);
    bool IsNumpadKeyThatIsAffectedByShift(const DWORD vkCode);
    DWORD GetNumpadOriginEncodingBit();

    // Stable identity for one physical press. Unlike vkCode, scan code and the
    // extended bit do not change when Shift or Num Lock changes a numpad key alias.
    std::optional<size_t> GetPhysicalKeyEventIndex(const LowlevelKeyboardEvent* data) noexcept;

    // Function to check if the key is a modifier key
    bool IsModifierKey(DWORD key);

    // Function to get the combined key for modifier keys
    DWORD GetCombinedKey(DWORD key);

    // Function to get the type of the key
    KeyType GetKeyType(DWORD key);

    // Function to set the value of a key event based on the arguments
    void SetKeyEvent(std::vector<INPUT>& keyEventArray, DWORD inputType, WORD keyCode, DWORD flags, ULONG_PTR extraInfo);

    // Appends one text input unit. Newlines use Shift+Enter so chat-style controls
    // insert a line break instead of submitting their contents.
    void SetTextInputUnit(std::vector<INPUT>& inputArray, wchar_t value, ULONG_PTR extraInfo);

    // Function to set the dummy key events used for remapping shortcuts, required to ensure releasing a modifier doesn't trigger another action (For example, Win->Start Menu or Alt->Menu bar)
    void SetDummyKeyEvent(std::vector<INPUT>& keyEventArray, ULONG_PTR extraInfo);

    // Function to send text input directly, with multiline support.
    // Sends each line via KEYEVENTF_UNICODE and newlines via Shift+Enter
    // as separate SendInput calls to avoid mixing event types.
    void SendTextInput(const std::wstring& text, KeyboardManagerInput::InputInterface& ii);

    // Function to return window handle for a full screen UWP app
    HWND GetFullscreenUWPWindowHandle();

    // Function to return the executable name of the application in focus
    std::wstring GetCurrentApplication(bool keepPath);

    // Function to set key events for modifier keys: When shortcutToCompare is passed (non-empty shortcut), then the key event is sent only if both shortcut's don't have the same modifier key. When keyToBeReleased is passed (non-NULL), then the key event is sent if either the shortcuts don't have the same modifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
    void SetModifierKeyEvents(const Shortcut& shortcutToBeSent, const Modifiers& modifiersKeys, std::vector<INPUT>& keyEventArray, bool isKeyDown, ULONG_PTR extraInfoFlag, const Shortcut& shortcutToCompare = Shortcut(), const DWORD& keyToBeReleased = NULL);


    // Function to filter the key codes for artificial key codes
    int32_t FilterArtificialKeys(const int32_t& key);

    // Function to sort a vector of shortcuts based on its size
    void SortShortcutVectorBasedOnSize(std::vector<Shortcut>& shortcutVector);
}
