#include "pch.h"
#include "Helpers.h"
#include <sstream>

#include <common/interop/shared_constants.h>
#include <common/utils/process_path.h>

#include "KeyboardManagerConstants.h"

namespace Helpers
{
    DWORD EncodeKeyNumpadOrigin(const DWORD key, const bool extended)
    {
        bool numpad_originated = false;
        switch (key)
        {
        case VK_LEFT:
        case VK_RIGHT:
        case VK_UP:
        case VK_DOWN:
        case VK_INSERT:
        case VK_DELETE:
        case VK_PRIOR:
        case VK_NEXT:
        case VK_HOME:
        case VK_END:
            numpad_originated = !extended;
            break;
        case VK_RETURN:
        case VK_DIVIDE:
            numpad_originated = extended;
            break;
        }

        if (numpad_originated)
            return key | GetNumpadOriginEncodingBit();
        else
            return key;
    }

    DWORD ClearKeyNumpadOrigin(const DWORD key)
    {
        return (key & ~GetNumpadOriginEncodingBit());
    }

    bool IsNumpadOriginated(const DWORD key)
    {
        return !!(key & GetNumpadOriginEncodingBit());
    }
    DWORD GetNumpadOriginEncodingBit()
    {
        // Intentionally do not mimic KF_EXTENDED to avoid confusion, because it's not the same thing
        // See EncodeKeyNumpadOrigin.
        return 1ull << 31;
    }
    // Function to check if the key is a modifier key
    bool IsModifierKey(DWORD key)
    {
        return (GetKeyType(key) != KeyType::Action);
    }

    // Function to get the combined key for modifier keys
    DWORD GetCombinedKey(DWORD key)
    {
        switch (key)
        {
        case VK_LWIN:
        case VK_RWIN:
            return CommonSharedConstants::VK_WIN_BOTH;
        case VK_LCONTROL:
        case VK_RCONTROL:
            return VK_CONTROL;
        case VK_LMENU:
        case VK_RMENU:
            return VK_MENU;
        case VK_LSHIFT:
        case VK_RSHIFT:
            return VK_SHIFT;
        default:
            return key;
        }
    }

    // Function to get the type of the key
    KeyType GetKeyType(DWORD key)
    {
        switch (key)
        {
        case CommonSharedConstants::VK_WIN_BOTH:
        case VK_LWIN:
        case VK_RWIN:
            return KeyType::Win;
        case VK_CONTROL:
        case VK_LCONTROL:
        case VK_RCONTROL:
            return KeyType::Ctrl;
        case VK_MENU:
        case VK_LMENU:
        case VK_RMENU:
            return KeyType::Alt;
        case VK_SHIFT:
        case VK_LSHIFT:
        case VK_RSHIFT:
            return KeyType::Shift;
        default:
            return KeyType::Action;
        }
    }

    // Function to return if the key is an extended key which requires the use of the extended key flag
    bool IsExtendedKey(DWORD key)
    {
        switch (key)
        {
        case VK_RCONTROL:
        case VK_RMENU:
        case VK_NUMLOCK:
        case VK_SNAPSHOT:
        case VK_CANCEL:
            // If the extended flag is not set for the following keys, their NumPad versions are sent. This causes weird behavior when NumLock is on (more information at https://github.com/microsoft/PowerToys/issues/3478)
        case VK_INSERT:
        case VK_HOME:
        case VK_PRIOR:
        case VK_DELETE:
        case VK_END:
        case VK_NEXT:
        case VK_LEFT:
        case VK_DOWN:
        case VK_RIGHT:
        case VK_UP:
        case VK_SLEEP:
        case VK_MEDIA_NEXT_TRACK:
        case VK_MEDIA_PREV_TRACK:
        case VK_MEDIA_STOP:
        case VK_MEDIA_PLAY_PAUSE:
        case VK_VOLUME_MUTE:
        case VK_VOLUME_UP:
        case VK_VOLUME_DOWN:
        case VK_LAUNCH_MEDIA_SELECT:
        case VK_LAUNCH_MAIL:
        case VK_LAUNCH_APP1:
        case VK_LAUNCH_APP2:
        case VK_BROWSER_SEARCH:
        case VK_BROWSER_HOME:
        case VK_BROWSER_BACK:
        case VK_BROWSER_FORWARD:
        case VK_BROWSER_STOP:
        case VK_BROWSER_REFRESH:
        case VK_BROWSER_FAVORITES:
            return true;
        default:
            return false;
        }
    }

    // Function to set the value of a key event based on the arguments
    void SetKeyEvent(std::vector<INPUT>& keyEventArray, DWORD inputType, WORD keyCode, DWORD flags, ULONG_PTR extraInfo)
    {
        INPUT keyEvent{};
        keyEvent.type = inputType;
        keyEvent.ki.wVk = keyCode;
        keyEvent.ki.dwFlags = flags;
        if (IsExtendedKey(keyCode))
        {
            keyEvent.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
        }
        keyEvent.ki.dwExtraInfo = extraInfo;

        // Set wScan to the value from MapVirtualKey as some applications may use the scan code for handling input, for instance, Windows Terminal ignores non-character input which has scancode set to 0.
        // MapVirtualKey returns 0 if the key code does not correspond to a physical key (such as unassigned/reserved keys). More details at https://github.com/microsoft/PowerToys/pull/7143#issue-498877747
        keyEvent.ki.wScan = static_cast<WORD>(MapVirtualKey(keyCode, MAPVK_VK_TO_VSC));
        keyEventArray.push_back(keyEvent);
    }

    // Function to set the dummy key events used for remapping shortcuts, required to ensure releasing a modifier doesn't trigger another action (For example, Win->Start Menu or Alt->Menu bar)
    void SetDummyKeyEvent(std::vector<INPUT>& keyEventArray, ULONG_PTR extraInfo)
    {
        SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(KeyboardManagerConstants::DUMMY_KEY), 0, extraInfo);
        SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(KeyboardManagerConstants::DUMMY_KEY), KEYEVENTF_KEYUP, extraInfo);
    }

    // Function to return window handle for a full screen UWP app
    HWND GetFullscreenUWPWindowHandle()
    {
        // Using GetGUIThreadInfo for getting the process of the window in focus. GetForegroundWindow has issues with UWP apps as it returns the Application Frame Host as its linked process
        GUITHREADINFO guiThreadInfo;
        guiThreadInfo.cbSize = sizeof(GUITHREADINFO);
        GetGUIThreadInfo(0, &guiThreadInfo);

        // If no window in focus, use the active window
        if (guiThreadInfo.hwndFocus == nullptr)
        {
            return guiThreadInfo.hwndActive;
        }
        return guiThreadInfo.hwndFocus;
    }

    // Function to return the executable name of the application in focus
    std::wstring GetCurrentApplication(bool keepPath)
    {
        HWND current_window_handle = GetForegroundWindow();
        std::wstring process_name;

        if (current_window_handle != nullptr)
        {
            std::wstring process_path = get_process_path(current_window_handle);
            process_name = process_path;

            // Get process name from path
            PathStripPath(&process_path[0]);

            // Remove elements after null character
            process_path.erase(std::find(process_path.begin(), process_path.end(), L'\0'), process_path.end());

            // If the UWP app is in full-screen, then using GetForegroundWindow approach might fail
            if (process_path == L"ApplicationFrameHost.exe")
            {
                HWND fullscreen_window_handle = GetFullscreenUWPWindowHandle();
                if (fullscreen_window_handle != nullptr)
                {
                    process_path = get_process_path(fullscreen_window_handle);
                    process_name = process_path;

                    // Get process name from path
                    PathStripPath(&process_path[0]);

                    // Remove elements after null character
                    process_path.erase(std::find(process_path.begin(), process_path.end(), L'\0'), process_path.end());
                }
            }

            // If keepPath is false, then return only the name of the process
            if (!keepPath)
            {
                process_name = process_path;
            }
        }

        return process_name;
    }

    // Function to set key events for modifier keys: When shortcutToCompare is passed (non-empty shortcut), then the key event is sent only if both shortcut's don't have the same modifier key. When keyToBeReleased is passed (non-NULL), then the key event is sent if either the shortcuts don't have the same modifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
    void SetModifierKeyEvents(const Shortcut& shortcutToBeSent, const ModifierKey& winKeyInvoked, std::vector<INPUT>& keyEventArray, bool isKeyDown, ULONG_PTR extraInfoFlag, const Shortcut& shortcutToCompare, const DWORD& keyToBeReleased)
    {
        // If key down is to be sent, send in the order Win, Ctrl, Alt, Shift
        if (isKeyDown)
        {
            // If shortcutToCompare is non-empty, then the key event is sent only if both shortcut's don't have the same modifier key. If keyToBeReleased is non-NULL, then the key event is sent if either the shortcuts don't have the same modifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
            if (shortcutToBeSent.GetWinKey(winKeyInvoked) != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetWinKey(winKeyInvoked) != shortcutToCompare.GetWinKey(winKeyInvoked)) && (keyToBeReleased == NULL || !shortcutToBeSent.CheckWinKey(keyToBeReleased)))
            {
                Helpers::SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(shortcutToBeSent.GetWinKey(winKeyInvoked)), 0, extraInfoFlag);
            }
            if (shortcutToBeSent.GetCtrlKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetCtrlKey() != shortcutToCompare.GetCtrlKey()) && (keyToBeReleased == NULL || !shortcutToBeSent.CheckCtrlKey(keyToBeReleased)))
            {
                Helpers::SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(shortcutToBeSent.GetCtrlKey()), 0, extraInfoFlag);
            }
            if (shortcutToBeSent.GetAltKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetAltKey() != shortcutToCompare.GetAltKey()) && (keyToBeReleased == NULL || !shortcutToBeSent.CheckAltKey(keyToBeReleased)))
            {
                Helpers::SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(shortcutToBeSent.GetAltKey()), 0, extraInfoFlag);
            }
            if (shortcutToBeSent.GetShiftKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetShiftKey() != shortcutToCompare.GetShiftKey()) && (keyToBeReleased == NULL || !shortcutToBeSent.CheckShiftKey(keyToBeReleased)))
            {
                Helpers::SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(shortcutToBeSent.GetShiftKey()), 0, extraInfoFlag);
            }
        }

        // If key up is to be sent, send in the order Shift, Alt, Ctrl, Win
        else
        {
            // If shortcutToCompare is non-empty, then the key event is sent only if both shortcut's don't have the same modifier key. If keyToBeReleased is non-NULL, then the key event is sent if either the shortcuts don't have the same modifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
            if (shortcutToBeSent.GetShiftKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetShiftKey() != shortcutToCompare.GetShiftKey() || shortcutToBeSent.CheckShiftKey(keyToBeReleased)))
            {
                Helpers::SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(shortcutToBeSent.GetShiftKey()), KEYEVENTF_KEYUP, extraInfoFlag);
            }
            if (shortcutToBeSent.GetAltKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetAltKey() != shortcutToCompare.GetAltKey() || shortcutToBeSent.CheckAltKey(keyToBeReleased)))
            {
                Helpers::SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(shortcutToBeSent.GetAltKey()), KEYEVENTF_KEYUP, extraInfoFlag);
            }
            if (shortcutToBeSent.GetCtrlKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetCtrlKey() != shortcutToCompare.GetCtrlKey() || shortcutToBeSent.CheckCtrlKey(keyToBeReleased)))
            {
                Helpers::SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(shortcutToBeSent.GetCtrlKey()), KEYEVENTF_KEYUP, extraInfoFlag);
            }
            if (shortcutToBeSent.GetWinKey(winKeyInvoked) != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetWinKey(winKeyInvoked) != shortcutToCompare.GetWinKey(winKeyInvoked) || shortcutToBeSent.CheckWinKey(keyToBeReleased)))
            {
                Helpers::SetKeyEvent(keyEventArray, INPUT_KEYBOARD, static_cast<WORD>(shortcutToBeSent.GetWinKey(winKeyInvoked)), KEYEVENTF_KEYUP, extraInfoFlag);
            }
        }
    }

    void SetTextKeyEvents(std::vector<INPUT>& keyEventArray, const std::wstring& remapping)
    {
        for (wchar_t c : remapping)
        {
            for (DWORD flag : { 0, KEYEVENTF_KEYUP })
            {
                INPUT input{};
                input.type = INPUT_KEYBOARD;
                input.ki.dwFlags = KEYEVENTF_UNICODE | flag;
                input.ki.dwExtraInfo = KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG;
                input.ki.wScan = c;
                keyEventArray.push_back(input);
            }
        }
    }

    // Function to filter the key codes for artificial key codes
    int32_t FilterArtificialKeys(const int32_t& key)
    {
        switch (key)
        {
        // If a key is remapped to VK_WIN_BOTH, we send VK_LWIN instead
        case CommonSharedConstants::VK_WIN_BOTH:
            return VK_LWIN;
        }

        return key;
    }

    // Function to sort a vector of shortcuts based on it's size
    void SortShortcutVectorBasedOnSize(std::vector<Shortcut>& shortcutVector)
    {
        std::sort(shortcutVector.begin(), shortcutVector.end(), [](Shortcut first, Shortcut second) {
            return first.Size() > second.Size();
        });
    }
}
