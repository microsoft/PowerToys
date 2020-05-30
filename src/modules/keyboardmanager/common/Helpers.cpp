#include "pch.h"
#include "Helpers.h"
#include <sstream>
#include "../common/shared_constants.h"
#include <shlwapi.h>

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
            return L"Cannot remap a key more than once";
        case ErrorType::MapToSameKey:
            return L"Cannot remap a key to itself";
        case ErrorType::ConflictingModifierKey:
            return L"Cannot remap this key as it conflicts with another remapped key";
        case ErrorType::SameShortcutPreviouslyMapped:
            return L"Cannot remap a shortcut more than once";
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

    // Function to return the window in focus
    HWND GetFocusWindowHandle()
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
        HWND current_window_handle = GetFocusWindowHandle();
        DWORD process_id;
        DWORD nSize = MAX_PATH;
        WCHAR buffer[MAX_PATH] = { 0 };

        // Get process ID of the focus window
        DWORD thread_id = GetWindowThreadProcessId(current_window_handle, &process_id);
        HANDLE hProc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, process_id);

        // Get full path of the executable
        bool res = QueryFullProcessImageName(hProc, 0, buffer, &nSize);
        std::wstring process_name;
        CloseHandle(hProc);

        process_name = buffer;
        if (res)
        {
            PathStripPath(buffer);

            if (!keepPath)
            {
                process_name = buffer;
            }
        }
        return process_name;
    }
}
