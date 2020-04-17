#pragma once
#include <interface/lowlevel_keyboard_event_data.h>
#include <string>
#include <map>
#include <set>
#include <mutex>
#include <windows.h>

using namespace winrt;

// Wrapper class to handle keyboard layout
class LayoutMap
{
private:
    // Stores mappings for all the virtual key codes to the name of the key
    std::mutex keyboardLayoutMap_mutex;

    // Stores the previous layout
    HKL previousLayout = 0;

    // Stores the keys which have a unicode representation
    std::set<std::pair<DWORD, std::wstring>> unicodeKeys;

    // Stores the keys which do not have a name
    std::set<std::pair<DWORD, std::wstring>> unknownKeys;

public:
    std::map<DWORD, std::wstring> keyboardLayoutMap;
    // Update Keyboard layout according to input locale identifier
    void UpdateLayout();

    LayoutMap()
    {
        UpdateLayout();
    }

    // Function to return the unicode string name of the key
    std::wstring GetKeyName(DWORD key);

    // Function to return two vectors: the list of names of all the keys for the drop down, and their corresponding virtual key codes. If the first argument is true, then an additional None option is added at the top
    std::pair<Windows::Foundation::Collections::IVector<Windows::Foundation::IInspectable>, std::vector<DWORD>> GetKeyList(const bool isShortcut = false);
};
