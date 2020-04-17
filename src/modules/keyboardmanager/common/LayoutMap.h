#pragma once
#include <interface/lowlevel_keyboard_event_data.h>
#include <string>
#include <map>
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
    std::map<DWORD, std::wstring> unicodeKeys;

    // Stores the keys which do not have a name
    std::map<DWORD, std::wstring> unknownKeys;

    // Stores true if the fixed ordering key code list has already been set
    bool isKeyCodeListGenerated = false;

    // Stores a fixed order key code list for the drop down menus. It is kept fixed to change in ordering due to languages
    std::vector<DWORD> keyCodeList;

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

    // Function to return the list of key codes in the order for the drop down. It creates it if it doesn't exist
    std::vector<DWORD> GetKeyCodeList(const bool isShortcut = false);

    // Function to return the list of key name in the order for the drop down based on the key codes
    Windows::Foundation::Collections::IVector<Windows::Foundation::IInspectable> GetKeyNameList(const bool isShortcut = false);
};
