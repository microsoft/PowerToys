#include "pch.h"
#include "Helpers.h"
#include <sstream>
#include "../common/shared_constants.h"
#include <shlwapi.h>
#include "../common/keyboard_layout.h"

using namespace winrt::Windows::Foundation;

namespace KeyboardManagerHelper
{
    // Function to split a wstring based on a delimiter and return a vector of split strings
    std::vector<std::wstring> splitwstring(const std::wstring& input, wchar_t delimiter)
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

    // Function to return the next sibling element for an element under a stack panel
    IInspectable getSiblingElement(IInspectable const& element)
    {
        FrameworkElement frameworkElement = element.as<FrameworkElement>();
        StackPanel parentElement = frameworkElement.Parent().as<StackPanel>();
        uint32_t index;

        parentElement.Children().IndexOf(frameworkElement, index);
        return parentElement.Children().GetAt(index + 1);
    }

    // Function to check if the key is a modifier key
    bool IsModifierKey(DWORD key)
    {
        return (GetKeyType(key) != KeyType::Action);
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
            return true;
        default:
            return false;
        }
    }

    Collections::IVector<IInspectable> ToBoxValue(const std::vector<std::wstring>& list)
    {
        Collections::IVector<IInspectable> boxList = single_threaded_vector<IInspectable>();
        for (auto& val : list)
        {
            boxList.Append(winrt::box_value(val));
        }

        return boxList;
    }

    // Function to check if two keys are equal or cover the same set of keys. Return value depends on type of overlap
    ErrorType DoKeysOverlap(DWORD first, DWORD second)
    {
        // If the keys are same
        if (first == second)
        {
            return ErrorType::SameKeyPreviouslyMapped;
        }
        else if ((GetKeyType(first) == GetKeyType(second)) && GetKeyType(first) != KeyType::Action)
        {
            // If the keys are of the same modifier type and overlapping, i.e. one is L/R and other is common
            if (((first == VK_LWIN && second == VK_RWIN) || (first == VK_RWIN && second == VK_LWIN)) || ((first == VK_LCONTROL && second == VK_RCONTROL) || (first == VK_RCONTROL && second == VK_LCONTROL)) || ((first == VK_LMENU && second == VK_RMENU) || (first == VK_RMENU && second == VK_LMENU)) || ((first == VK_LSHIFT && second == VK_RSHIFT) || (first == VK_RSHIFT && second == VK_LSHIFT)))
            {
                return ErrorType::NoError;
            }
            else
            {
                return ErrorType::ConflictingModifierKey;
            }
        }
        // If no overlap
        else
        {
            return ErrorType::NoError;
        }
    }

    // Function to return the error message
    winrt::hstring GetErrorMessage(ErrorType errorType)
    {
        switch (errorType)
        {
        case ErrorType::NoError:
            return L"Remapping successful";
        case ErrorType::SameKeyPreviouslyMapped:
            return L"Cannot remap a key more than once for the same target app";
        case ErrorType::MapToSameKey:
            return L"Cannot remap a key to itself";
        case ErrorType::ConflictingModifierKey:
            return L"Cannot remap this key as it conflicts with another remapped key";
        case ErrorType::SameShortcutPreviouslyMapped:
            return L"Cannot remap a shortcut more than once for the same target app";
        case ErrorType::MapToSameShortcut:
            return L"Cannot remap a shortcut to itself";
        case ErrorType::ConflictingModifierShortcut:
            return L"Cannot remap this shortcut as it conflicts with another remapped shortcut";
        case ErrorType::WinL:
            return L"Cannot remap from/to Win L";
        case ErrorType::CtrlAltDel:
            return L"Cannot remap from/to Ctrl Alt Del";
        case ErrorType::RemapUnsuccessful:
            return L"Some remappings were not applied";
        case ErrorType::SaveFailed:
            return L"Failed to save the remappings";
        case ErrorType::MissingKey:
            return L"Incomplete remapping";
        case ErrorType::ShortcutStartWithModifier:
            return L"Shortcut must start with a modifier key";
        case ErrorType::ShortcutCannotHaveRepeatedModifier:
            return L"Shortcut cannot contain a repeated modifier";
        case ErrorType::ShortcutAtleast2Keys:
            return L"Shortcut must have atleast 2 keys";
        case ErrorType::ShortcutOneActionKey:
            return L"Shortcut must contain an action key";
        case ErrorType::ShortcutNotMoreThanOneActionKey:
            return L"Shortcut cannot have more than one action key";
        case ErrorType::ShortcutMaxShortcutSizeOneActionKey:
            return L"Shortcuts can only have up to 2 modifier keys";
        default:
            return L"Unexpected error";
        }
    }

    // Function to set the value of a key event based on the arguments
    void SetKeyEvent(LPINPUT keyEventArray, int index, DWORD inputType, WORD keyCode, DWORD flags, ULONG_PTR extraInfo)
    {
        keyEventArray[index].type = inputType;
        keyEventArray[index].ki.wVk = keyCode;
        keyEventArray[index].ki.dwFlags = flags;
        if (IsExtendedKey(keyCode))
        {
            keyEventArray[index].ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
        }
        keyEventArray[index].ki.dwExtraInfo = extraInfo;
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

    // Function to set key events for modifier keys: When shortcutToCompare is passed (non-empty shortcut), then the key event is sent only if both shortcut's don't have the same modifier key. When keyToBeReleased is passed (non-NULL), then the key event is sent if either the shortcuts don't have the same modfifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
    void SetModifierKeyEvents(const Shortcut& shortcutToBeSent, const ModifierKey& winKeyInvoked, LPINPUT keyEventArray, int& index, bool isKeyDown, ULONG_PTR extraInfoFlag, const Shortcut& shortcutToCompare, const DWORD& keyToBeReleased)
    {
        // If key down is to be sent, send in the order Win, Ctrl, Alt, Shift
        if (isKeyDown)
        {
            // If shortcutToCompare is non-empty, then the key event is sent only if both shortcut's don't have the same modifier key. If keyToBeReleased is non-NULL, then the key event is sent if either the shortcuts don't have the same modfifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
            if (shortcutToBeSent.GetWinKey(winKeyInvoked) != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetWinKey(winKeyInvoked) != shortcutToCompare.GetWinKey(winKeyInvoked)) && (keyToBeReleased == NULL || !shortcutToBeSent.CheckWinKey(keyToBeReleased)))
            {
                KeyboardManagerHelper::SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)shortcutToBeSent.GetWinKey(winKeyInvoked), 0, extraInfoFlag);
                index++;
            }
            if (shortcutToBeSent.GetCtrlKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetCtrlKey() != shortcutToCompare.GetCtrlKey()) && (keyToBeReleased == NULL || !shortcutToBeSent.CheckCtrlKey(keyToBeReleased)))
            {
                KeyboardManagerHelper::SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)shortcutToBeSent.GetCtrlKey(), 0, extraInfoFlag);
                index++;
            }
            if (shortcutToBeSent.GetAltKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetAltKey() != shortcutToCompare.GetAltKey()) && (keyToBeReleased == NULL || !shortcutToBeSent.CheckAltKey(keyToBeReleased)))
            {
                KeyboardManagerHelper::SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)shortcutToBeSent.GetAltKey(), 0, extraInfoFlag);
                index++;
            }
            if (shortcutToBeSent.GetShiftKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetShiftKey() != shortcutToCompare.GetShiftKey()) && (keyToBeReleased == NULL || !shortcutToBeSent.CheckShiftKey(keyToBeReleased)))
            {
                KeyboardManagerHelper::SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)shortcutToBeSent.GetShiftKey(), 0, extraInfoFlag);
                index++;
            }
        }

        // If key up is to be sent, send in the order Shift, Alt, Ctrl, Win
        else
        {
            // If shortcutToCompare is non-empty, then the key event is sent only if both shortcut's don't have the same modifier key. If keyToBeReleased is non-NULL, then the key event is sent if either the shortcuts don't have the same modfifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
            if (shortcutToBeSent.GetShiftKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetShiftKey() != shortcutToCompare.GetShiftKey() || shortcutToBeSent.CheckShiftKey(keyToBeReleased)))
            {
                KeyboardManagerHelper::SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)shortcutToBeSent.GetShiftKey(), KEYEVENTF_KEYUP, extraInfoFlag);
                index++;
            }
            if (shortcutToBeSent.GetAltKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetAltKey() != shortcutToCompare.GetAltKey() || shortcutToBeSent.CheckAltKey(keyToBeReleased)))
            {
                KeyboardManagerHelper::SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)shortcutToBeSent.GetAltKey(), KEYEVENTF_KEYUP, extraInfoFlag);
                index++;
            }
            if (shortcutToBeSent.GetCtrlKey() != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetCtrlKey() != shortcutToCompare.GetCtrlKey() || shortcutToBeSent.CheckCtrlKey(keyToBeReleased)))
            {
                KeyboardManagerHelper::SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)shortcutToBeSent.GetCtrlKey(), KEYEVENTF_KEYUP, extraInfoFlag);
                index++;
            }
            if (shortcutToBeSent.GetWinKey(winKeyInvoked) != NULL && (shortcutToCompare.IsEmpty() || shortcutToBeSent.GetWinKey(winKeyInvoked) != shortcutToCompare.GetWinKey(winKeyInvoked) || shortcutToBeSent.CheckWinKey(keyToBeReleased)))
            {
                KeyboardManagerHelper::SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)shortcutToBeSent.GetWinKey(winKeyInvoked), KEYEVENTF_KEYUP, extraInfoFlag);
                index++;
            }
        }
    }

    // Function to filter the key codes for artificial key codes
    DWORD FilterArtificialKeys(const DWORD& key)
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

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(std::vector<DWORD>& currentKeys, int selectedKeyIndex, const std::vector<DWORD>& keyCodeList)
    {
        // check if modifier has already been added before in a previous drop down
        int currentDropDownIndex = -1;

        // Find the key index of the current drop down selection so that we skip that index while searching for repeated modifiers
        for (int i = 0; i < currentKeys.size(); i++)
        {
            if (currentKeys[i] == keyCodeList[selectedKeyIndex])
            {
                currentDropDownIndex = i;
                break;
            }
        }

        bool matchPreviousModifier = false;
        for (int i = 0; i < currentKeys.size(); i++)
        {
            // Skip the current drop down
            if (i != currentDropDownIndex)
            {
                // If the key type for the newly added key matches any of the existing keys in the shortcut
                if (KeyboardManagerHelper::GetKeyType(keyCodeList[selectedKeyIndex]) == KeyboardManagerHelper::GetKeyType(currentKeys[i]))
                {
                    matchPreviousModifier = true;
                    break;
                }
            }
        }

        return matchPreviousModifier;
    }

    // Function to get the selected key codes from the list of selected indices
    std::vector<DWORD> GetKeyCodesFromSelectedIndices(const std::vector<int32_t>& selectedIndices, const std::vector<DWORD>& keyCodeList)
    {
        std::vector<DWORD> keys;

        for (int i = 0; i < selectedIndices.size(); i++)
        {
            int selectedKeyIndex = selectedIndices[i];
            if (selectedKeyIndex != -1 && keyCodeList.size() > selectedKeyIndex)
            {
                // If None is not the selected key
                if (keyCodeList[selectedKeyIndex] != 0)
                {
                    keys.push_back(keyCodeList[selectedKeyIndex]);
                }
            }
        }

        return keys;
    }
}
