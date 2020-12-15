#include "pch.h"
#include "Helpers.h"
#include <sstream>

#include <common/interop/keyboard_layout.h>
#include <common/interop/shared_constants.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>

#include <shlwapi.h>
#include "keyboardmanager/dll/Generated Files/resource.h"
#include <common/interop/keyboard_layout.h>
#include "KeyboardManagerConstants.h"

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

    Collections::IVector<IInspectable> ToBoxValue(const std::vector<std::pair<DWORD, std::wstring>>& list)
    {
        Collections::IVector<IInspectable> boxList = single_threaded_vector<IInspectable>();
        for (auto& val : list)
        {
            auto comboBox = ComboBoxItem();
            comboBox.DataContext(winrt::box_value(std::to_wstring(val.first)));
            comboBox.Content(winrt::box_value(val.second));
            boxList.Append(winrt::box_value(comboBox));
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
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_REMAPSUCCESSFUL).c_str();
        case ErrorType::SameKeyPreviouslyMapped:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SAMEKEYPREVIOUSLYMAPPED).c_str();
        case ErrorType::MapToSameKey:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_MAPPEDTOSAMEKEY).c_str();
        case ErrorType::ConflictingModifierKey:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_CONFLICTINGMODIFIERKEY).c_str();
        case ErrorType::SameShortcutPreviouslyMapped:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SAMESHORTCUTPREVIOUSLYMAPPED).c_str();
        case ErrorType::MapToSameShortcut:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_MAPTOSAMESHORTCUT).c_str();
        case ErrorType::ConflictingModifierShortcut:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_CONFLICTINGMODIFIERSHORTCUT).c_str();
        case ErrorType::WinL:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_WINL).c_str();
        case ErrorType::CtrlAltDel:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_CTRLALTDEL).c_str();
        case ErrorType::RemapUnsuccessful:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_REMAPUNSUCCESSFUL).c_str();
        case ErrorType::SaveFailed:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SAVEFAILED).c_str();
        case ErrorType::ShortcutStartWithModifier:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTSTARTWITHMODIFIER).c_str();
        case ErrorType::ShortcutCannotHaveRepeatedModifier:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTNOREPEATEDMODIFIER).c_str();
        case ErrorType::ShortcutAtleast2Keys:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTATLEAST2KEYS).c_str();
        case ErrorType::ShortcutOneActionKey:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTONEACTIONKEY).c_str();
        case ErrorType::ShortcutNotMoreThanOneActionKey:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTMAXONEACTIONKEY).c_str();
        case ErrorType::ShortcutMaxShortcutSizeOneActionKey:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_MAXSHORTCUTSIZE).c_str();
        case ErrorType::ShortcutDisableAsActionKey:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_DISABLEASACTIONKEY).c_str();
        default:
            return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_DEFAULT).c_str();
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

        // Set wScan to the value from MapVirtualKey as some applications may use the scan code for handling input, for instance, Windows Terminal ignores non-character input which has scancode set to 0.
        // MapVirtualKey returns 0 if the key code does not correspond to a physical key (such as unassigned/reserved keys). More details at https://github.com/microsoft/PowerToys/pull/7143#issue-498877747
        keyEventArray[index].ki.wScan = (WORD)MapVirtualKey(keyCode, MAPVK_VK_TO_VSC);
    }

    // Function to set the dummy key events used for remapping shortcuts, required to ensure releasing a modifier doesn't trigger another action (For example, Win->Start Menu or Alt->Menu bar)
    void SetDummyKeyEvent(LPINPUT keyEventArray, int& index, ULONG_PTR extraInfo)
    {
        SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)KeyboardManagerConstants::DUMMY_KEY, 0, extraInfo);
        index++;
        SetKeyEvent(keyEventArray, index, INPUT_KEYBOARD, (WORD)KeyboardManagerConstants::DUMMY_KEY, KEYEVENTF_KEYUP, extraInfo);
        index++;
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
    void SetModifierKeyEvents(const Shortcut& shortcutToBeSent, const ModifierKey& winKeyInvoked, LPINPUT keyEventArray, int& index, bool isKeyDown, ULONG_PTR extraInfoFlag, const Shortcut& shortcutToCompare, const DWORD& keyToBeReleased)
    {
        // If key down is to be sent, send in the order Win, Ctrl, Alt, Shift
        if (isKeyDown)
        {
            // If shortcutToCompare is non-empty, then the key event is sent only if both shortcut's don't have the same modifier key. If keyToBeReleased is non-NULL, then the key event is sent if either the shortcuts don't have the same modifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
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
            // If shortcutToCompare is non-empty, then the key event is sent only if both shortcut's don't have the same modifier key. If keyToBeReleased is non-NULL, then the key event is sent if either the shortcuts don't have the same modifier or if the shortcutToBeSent's modifier matches the keyToBeReleased
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

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(const std::vector<int32_t>& currentKeys, int selectedKeyCode)
    {
        // Count the number of keys that are equal to 'selectedKeyCode'
        int numberOfSameType = 0;
        for (int i = 0; i < currentKeys.size(); i++)
        {
            numberOfSameType += KeyboardManagerHelper::GetKeyType(selectedKeyCode) == KeyboardManagerHelper::GetKeyType(currentKeys[i]);
        }

        // If we have at least two keys equal to 'selectedKeyCode' than modifier was repeated
        return numberOfSameType > 1;
    }

    winrt::Windows::Foundation::IInspectable GetWrapped(const winrt::Windows::Foundation::IInspectable& element, double width)
    {
        StackPanel sp = StackPanel();
        sp.Width(width);
        sp.Children().Append(element.as<FrameworkElement>());
        return sp;
    }
}
