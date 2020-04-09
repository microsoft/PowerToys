#pragma once
#include <interface/lowlevel_keyboard_event_data.h>
#include <string>
#include <map>
#include <mutex>
#include <windows.h>

// Wrapper class to handle keyboard layout
class LayoutMap
{
private:
    // Stores mappings for all the virtual key codes to the name of the key
    std::map<DWORD, std::wstring> keyboardLayoutMap;
    std::mutex keyboardLayoutMap_mutex;
    HKL previousLayout = 0;

public:
    // Update Keyboard layout according to input locale identifier
    void UpdateLayout();

    LayoutMap()
    {
        UpdateLayout();
    }

    // Function to return the unicode string name of the key
    std::wstring GetKeyName(DWORD key);

};
