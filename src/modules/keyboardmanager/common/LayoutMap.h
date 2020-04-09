#pragma once
#include <interface/lowlevel_keyboard_event_data.h>
#include <string>
#include <map>
#include <mutex>

// Wrapper class to handle keyboard layout
class LayoutMap
{
private:
    // Stores mappings for all the virtual key codes to the name of the key
    std::map<DWORD, std::wstring> keyboardLayoutMap;
    std::mutex keyboardLayoutMap_mutex;

public:
    LayoutMap()
    {
        // Get keyboard layout for current thread
        HKL layout = GetKeyboardLayout(0);
        unsigned char btKeys[256] = { 0 };
        GetKeyboardState(btKeys);

        // Iterate over all the virtual key codes
        for (int i = 0; i < 256; i++)
        {
            // Get the scan code from the virtual key code
            UINT scanCode = MapVirtualKeyExW(i, MAPVK_VK_TO_VSC, layout);
            // Get the unicode representation from the virtual key code and scan code pair to 
            wchar_t szBuffer[3] = { 0 };
            int result = ToUnicodeEx(i, scanCode, (BYTE*)btKeys, szBuffer, 3, 0, layout);
            // If a representation is returned
            if (result > 0)
            {
                keyboardLayoutMap[i] = szBuffer;
            }
            else
            {
                // Store the virtual key code as string
                keyboardLayoutMap[i] = L"VK " + std::to_wstring(i);
            }
        }

        // Override special key names like Shift, Ctrl etc because they don't have unicode mappings and key names like Enter, Space as they appear as "\r", " "
        // To do: localization
        keyboardLayoutMap[VK_CANCEL] = L"Break";
        keyboardLayoutMap[VK_BACK] = L"Backspace";
        keyboardLayoutMap[VK_TAB] = L"Tab";
        keyboardLayoutMap[VK_CLEAR] = L"Clear";
        keyboardLayoutMap[VK_RETURN] = L"Enter";
        keyboardLayoutMap[VK_SHIFT] = L"Shift";
        keyboardLayoutMap[VK_CONTROL] = L"Ctrl";
        keyboardLayoutMap[VK_MENU] = L"Alt";
        keyboardLayoutMap[VK_PAUSE] = L"Pause";
        keyboardLayoutMap[VK_CAPITAL] = L"Caps Lock";
        keyboardLayoutMap[VK_ESCAPE] = L"Esc";
        keyboardLayoutMap[VK_SPACE] = L"Space";
        keyboardLayoutMap[VK_PRIOR] = L"PgUp";
        keyboardLayoutMap[VK_NEXT] = L"PgDn";
        keyboardLayoutMap[VK_END] = L"End";
        keyboardLayoutMap[VK_HOME] = L"Home";
        keyboardLayoutMap[VK_LEFT] = L"Left";
        keyboardLayoutMap[VK_UP] = L"Up";
        keyboardLayoutMap[VK_RIGHT] = L"Right";
        keyboardLayoutMap[VK_DOWN] = L"Down";
        keyboardLayoutMap[VK_SELECT] = L"Select";
        keyboardLayoutMap[VK_PRINT] = L"Print";
        keyboardLayoutMap[VK_EXECUTE] = L"Execute";
        keyboardLayoutMap[VK_SNAPSHOT] = L"Print Screen";
        keyboardLayoutMap[VK_INSERT] = L"Insert";
        keyboardLayoutMap[VK_DELETE] = L"Delete";
        keyboardLayoutMap[VK_HELP] = L"Help";
        keyboardLayoutMap[VK_LWIN] = L"LWin";
        keyboardLayoutMap[VK_RWIN] = L"RWin";
        keyboardLayoutMap[VK_APPS] = L"Menu";
        keyboardLayoutMap[VK_SLEEP] = L"Sleep";
        keyboardLayoutMap[VK_NUMPAD0] = L"NumPad 0";
        keyboardLayoutMap[VK_NUMPAD1] = L"NumPad 1";
        keyboardLayoutMap[VK_NUMPAD2] = L"NumPad 2";
        keyboardLayoutMap[VK_NUMPAD3] = L"NumPad 3";
        keyboardLayoutMap[VK_NUMPAD4] = L"NumPad 4";
        keyboardLayoutMap[VK_NUMPAD5] = L"NumPad 5";
        keyboardLayoutMap[VK_NUMPAD6] = L"NumPad 6";
        keyboardLayoutMap[VK_NUMPAD7] = L"NumPad 7";
        keyboardLayoutMap[VK_NUMPAD8] = L"NumPad 8";
        keyboardLayoutMap[VK_NUMPAD9] = L"NumPad 9";
        keyboardLayoutMap[VK_SEPARATOR] = L"Separator";
        keyboardLayoutMap[VK_F1] = L"F1";
        keyboardLayoutMap[VK_F2] = L"F2";
        keyboardLayoutMap[VK_F3] = L"F3";
        keyboardLayoutMap[VK_F4] = L"F4";
        keyboardLayoutMap[VK_F5] = L"F5";
        keyboardLayoutMap[VK_F6] = L"F6";
        keyboardLayoutMap[VK_F7] = L"F7";
        keyboardLayoutMap[VK_F8] = L"F8";
        keyboardLayoutMap[VK_F9] = L"F9";
        keyboardLayoutMap[VK_F10] = L"F10";
        keyboardLayoutMap[VK_F11] = L"F11";
        keyboardLayoutMap[VK_F12] = L"F12";
        keyboardLayoutMap[VK_F13] = L"F13";
        keyboardLayoutMap[VK_F14] = L"F14";
        keyboardLayoutMap[VK_F15] = L"F15";
        keyboardLayoutMap[VK_F16] = L"F16";
        keyboardLayoutMap[VK_F17] = L"F17";
        keyboardLayoutMap[VK_F18] = L"F18";
        keyboardLayoutMap[VK_F19] = L"F19";
        keyboardLayoutMap[VK_F20] = L"F20";
        keyboardLayoutMap[VK_F21] = L"F21";
        keyboardLayoutMap[VK_F22] = L"F22";
        keyboardLayoutMap[VK_F23] = L"F23";
        keyboardLayoutMap[VK_F24] = L"F24";
        keyboardLayoutMap[VK_NUMLOCK] = L"Num Lock";
        keyboardLayoutMap[VK_SCROLL] = L"Scroll Lock";
        keyboardLayoutMap[VK_LSHIFT] = L"LShift";
        keyboardLayoutMap[VK_RSHIFT] = L"RShift";
        keyboardLayoutMap[VK_LCONTROL] = L"LCtrl";
        keyboardLayoutMap[VK_RCONTROL] = L"RCtrl";
        keyboardLayoutMap[VK_LMENU] = L"LAlt";
        keyboardLayoutMap[VK_RMENU] = L"RAlt";
        keyboardLayoutMap[VK_BROWSER_BACK] = L"Browser Back";
        keyboardLayoutMap[VK_BROWSER_FORWARD] = L"Browser Forward";
        keyboardLayoutMap[VK_BROWSER_REFRESH] = L"Browser Refresh";
        keyboardLayoutMap[VK_BROWSER_STOP] = L"Browser Stop";
        keyboardLayoutMap[VK_BROWSER_SEARCH] = L"Browser Search";
        keyboardLayoutMap[VK_BROWSER_FAVORITES] = L"Browser Favorites";
        keyboardLayoutMap[VK_BROWSER_HOME] = L"Browser Start & Home";
        keyboardLayoutMap[VK_VOLUME_MUTE] = L"Volume Mute";
        keyboardLayoutMap[VK_VOLUME_DOWN] = L"Volume Down";
        keyboardLayoutMap[VK_VOLUME_UP] = L"Volume Up";
        keyboardLayoutMap[VK_MEDIA_NEXT_TRACK] = L"Next Track";
        keyboardLayoutMap[VK_MEDIA_PREV_TRACK] = L"Previous Track";
        keyboardLayoutMap[VK_MEDIA_STOP] = L"Stop Media";
        keyboardLayoutMap[VK_MEDIA_PLAY_PAUSE] = L"Play/Pause Media";
        keyboardLayoutMap[VK_LAUNCH_MAIL] = L"Start Mail";
        keyboardLayoutMap[VK_LAUNCH_MEDIA_SELECT] = L"Select Media";
        keyboardLayoutMap[VK_LAUNCH_APP1] = L"Start Application 1";
        keyboardLayoutMap[VK_LAUNCH_APP2] = L"Start Application 2";
        keyboardLayoutMap[VK_PACKET] = L"Packet";
        keyboardLayoutMap[VK_ATTN] = L"Attn";
        keyboardLayoutMap[VK_CRSEL] = L"CrSel";
        keyboardLayoutMap[VK_EXSEL] = L"ExSel";
        keyboardLayoutMap[VK_EREOF] = L"Erase EOF";
        keyboardLayoutMap[VK_PLAY] = L"Play";
        keyboardLayoutMap[VK_ZOOM] = L"Zoom";
        keyboardLayoutMap[VK_PA1] = L"PA1";
        keyboardLayoutMap[VK_OEM_CLEAR] = L"Clear";
        keyboardLayoutMap[0xFF] = L"Undefined";
        // To do: Add IME key names
    }

    // Function to return the unicode string name of the key
    std::wstring GetKeyName(DWORD key);
};
