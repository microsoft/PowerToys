#include "pch.h"
#include <array>
#include <algorithm>

#include "keyboard_layout_impl.h"
#include "shared_constants.h"

LayoutMap::LayoutMap() :
    impl(new LayoutMap::LayoutMapImpl())
{
}

LayoutMap::~LayoutMap()
{
    delete impl;
}

void LayoutMap::UpdateLayout()
{
    impl->UpdateLayout();
}

std::wstring LayoutMap::GetKeyName(DWORD key)
{
    return impl->GetKeyName(key);
}

std::vector<DWORD> LayoutMap::GetKeyCodeList(const bool isShortcut)
{
    return impl->GetKeyCodeList(isShortcut);
}

std::vector<std::pair<DWORD, std::wstring>> LayoutMap::GetKeyNameList(const bool isShortcut)
{
    return impl->GetKeyNameList(isShortcut);
}

// Function to return the unicode string name of the key
std::wstring LayoutMap::LayoutMapImpl::GetKeyName(DWORD key)
{
    std::wstring result = L"Undefined";
    std::lock_guard<std::mutex> lock(keyboardLayoutMap_mutex);
    UpdateLayout();

    auto it = keyboardLayoutMap.find(key);
    if (it != keyboardLayoutMap.end())
    {
        result = it->second;
    }
    return result;
}

bool mapKeycodeToUnicode(const int vCode, HKL layout, const BYTE* keyState, std::array<wchar_t, 3>& outBuffer)
{
    // Get the scan code from the virtual key code
    const UINT scanCode = MapVirtualKeyExW(vCode, MAPVK_VK_TO_VSC, layout);
    // Get the unicode representation from the virtual key code and scan code pair
    const UINT wFlags = 1 << 2; // If bit 2 is set, keyboard state is not changed (Windows 10, version 1607 and newer)
    const int result = ToUnicodeEx(vCode, scanCode, keyState, outBuffer.data(), static_cast<int>(outBuffer.size()), wFlags, layout);
    return result != 0;
}

// Update Keyboard layout according to input locale identifier
void LayoutMap::LayoutMapImpl::UpdateLayout()
{
    // Get keyboard layout for current thread
    const HKL layout = GetKeyboardLayout(0);
    if (layout == previousLayout)
    {
        return;
    }
    previousLayout = layout;
    if (!isKeyCodeListGenerated)
    {
        unicodeKeys.clear();
        unknownKeys.clear();
    }

    std::array<BYTE, 256> btKeys = { 0 };
    // Only set the Caps Lock key to on for the key names in uppercase
    btKeys[VK_CAPITAL] = 1;

    // Iterate over all the virtual key codes. virtual key 0 is not used
    for (int i = 1; i < 256; i++)
    {
        std::array<wchar_t, 3> szBuffer = { 0 };
        if (mapKeycodeToUnicode(i, layout, btKeys.data(), szBuffer))
        {
            keyboardLayoutMap[i] = szBuffer.data();
            if (!isKeyCodeListGenerated)
            {
                unicodeKeys[i] = szBuffer.data();
            }
            continue;
        }

        // Store the virtual key code as string
        std::wstring vk = L"VK ";
        vk += std::to_wstring(i);
        keyboardLayoutMap[i] = vk;
        if (!isKeyCodeListGenerated)
        {
            unknownKeys[i] = vk;
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
    keyboardLayoutMap[VK_LWIN] = L"Win (Left)";
    keyboardLayoutMap[VK_RWIN] = L"Win (Right)";
    keyboardLayoutMap[VK_APPS] = L"Apps/Menu";
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
    keyboardLayoutMap[VK_LSHIFT] = L"Shift (Left)";
    keyboardLayoutMap[VK_RSHIFT] = L"Shift (Right)";
    keyboardLayoutMap[VK_LCONTROL] = L"Ctrl (Left)";
    keyboardLayoutMap[VK_RCONTROL] = L"Ctrl (Right)";
    keyboardLayoutMap[VK_LMENU] = L"Alt (Left)";
    keyboardLayoutMap[VK_RMENU] = L"Alt (Right)";
    keyboardLayoutMap[VK_BROWSER_BACK] = L"Browser Back";
    keyboardLayoutMap[VK_BROWSER_FORWARD] = L"Browser Forward";
    keyboardLayoutMap[VK_BROWSER_REFRESH] = L"Browser Refresh";
    keyboardLayoutMap[VK_BROWSER_STOP] = L"Browser Stop";
    keyboardLayoutMap[VK_BROWSER_SEARCH] = L"Browser Search";
    keyboardLayoutMap[VK_BROWSER_FAVORITES] = L"Browser Favorites";
    keyboardLayoutMap[VK_BROWSER_HOME] = L"Browser Home";
    keyboardLayoutMap[VK_VOLUME_MUTE] = L"Volume Mute";
    keyboardLayoutMap[VK_VOLUME_DOWN] = L"Volume Down";
    keyboardLayoutMap[VK_VOLUME_UP] = L"Volume Up";
    keyboardLayoutMap[VK_MEDIA_NEXT_TRACK] = L"Next Track";
    keyboardLayoutMap[VK_MEDIA_PREV_TRACK] = L"Previous Track";
    keyboardLayoutMap[VK_MEDIA_STOP] = L"Stop Media";
    keyboardLayoutMap[VK_MEDIA_PLAY_PAUSE] = L"Play/Pause Media";
    keyboardLayoutMap[VK_LAUNCH_MAIL] = L"Start Mail";
    keyboardLayoutMap[VK_LAUNCH_MEDIA_SELECT] = L"Select Media";
    keyboardLayoutMap[VK_LAUNCH_APP1] = L"Start App 1";
    keyboardLayoutMap[VK_LAUNCH_APP2] = L"Start App 2";
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
    keyboardLayoutMap[CommonSharedConstants::VK_WIN_BOTH] = L"Win";
    keyboardLayoutMap[VK_KANA] = L"IME Kana";
    keyboardLayoutMap[VK_HANGEUL] = L"IME Hangeul";
    keyboardLayoutMap[VK_HANGUL] = L"IME Hangul";
    keyboardLayoutMap[VK_JUNJA] = L"IME Junja";
    keyboardLayoutMap[VK_FINAL] = L"IME Final";
    keyboardLayoutMap[VK_HANJA] = L"IME Hanja";
    keyboardLayoutMap[VK_KANJI] = L"IME Kanji";
    keyboardLayoutMap[VK_CONVERT] = L"IME Convert";
    keyboardLayoutMap[VK_NONCONVERT] = L"IME Non-Convert";
    keyboardLayoutMap[VK_ACCEPT] = L"IME Kana";
    keyboardLayoutMap[VK_MODECHANGE] = L"IME Mode Change";
    keyboardLayoutMap[CommonSharedConstants::VK_DISABLED] = L"Disable";
}

// Function to return the list of key codes in the order for the drop down. It creates it if it doesn't exist
std::vector<DWORD> LayoutMap::LayoutMapImpl::GetKeyCodeList(const bool isShortcut)
{
    std::lock_guard<std::mutex> lock(keyboardLayoutMap_mutex);
    UpdateLayout();
    std::vector<DWORD> keyCodes;
    if (!isKeyCodeListGenerated)
    {
        // Add character keys
        for (auto& it : unicodeKeys)
        {
            // If it was not renamed with a special name
            if (it.second == keyboardLayoutMap[it.first])
            {
                keyCodes.push_back(it.first);
            }
        }

        // Add modifier keys in alphabetical order
        keyCodes.push_back(VK_MENU);
        keyCodes.push_back(VK_LMENU);
        keyCodes.push_back(VK_RMENU);
        keyCodes.push_back(VK_CONTROL);
        keyCodes.push_back(VK_LCONTROL);
        keyCodes.push_back(VK_RCONTROL);
        keyCodes.push_back(VK_SHIFT);
        keyCodes.push_back(VK_LSHIFT);
        keyCodes.push_back(VK_RSHIFT);
        keyCodes.push_back(CommonSharedConstants::VK_WIN_BOTH);
        keyCodes.push_back(VK_LWIN);
        keyCodes.push_back(VK_RWIN);

        // Add all other special keys
        std::vector<DWORD> specialKeys;
        for (int i = 1; i < 256; i++)
        {
            // If it is not already been added (i.e. it was either a modifier or had a unicode representation)
            if (std::find(keyCodes.begin(), keyCodes.end(), i) == keyCodes.end())
            {
                // If it is any other key but it is not named as VK #
                auto it = unknownKeys.find(i);
                if (it == unknownKeys.end())
                {
                    specialKeys.push_back(i);
                }
                else if (unknownKeys[i] != keyboardLayoutMap[i])
                {
                    specialKeys.push_back(i);
                }
            }
        }

        // Sort the special keys in alphabetical order
        std::sort(specialKeys.begin(), specialKeys.end(), [&](const DWORD& lhs, const DWORD& rhs) {
            return keyboardLayoutMap[lhs] < keyboardLayoutMap[rhs];
        });
        for (int i = 0; i < specialKeys.size(); i++)
        {
            keyCodes.push_back(specialKeys[i]);
        }

        // Add unknown keys
        for (auto& it : unknownKeys)
        {
            // If it was not renamed with a special name
            if (it.second == keyboardLayoutMap[it.first])
            {
                keyCodes.push_back(it.first);
            }
        }
        keyCodeList = keyCodes;
        isKeyCodeListGenerated = true;
    }
    else
    {
        keyCodes = keyCodeList;
    }

    // If it is a key list for the shortcut control then we add a "None" key at the start
    if (isShortcut)
    {
        keyCodes.insert(keyCodes.begin(), 0);
    }

    return keyCodes;
}

std::vector<std::pair<DWORD, std::wstring>> LayoutMap::LayoutMapImpl::GetKeyNameList(const bool isShortcut)
{
    std::vector<std::pair<DWORD, std::wstring>> keyNames;
    std::vector<DWORD> keyCodes = GetKeyCodeList(isShortcut);
    std::lock_guard<std::mutex> lock(keyboardLayoutMap_mutex);
    // If it is a key list for the shortcut control then we add a "None" key at the start
    if (isShortcut)
    {
        keyNames.push_back({ 0, L"None" });
        for (int i = 1; i < keyCodes.size(); i++)
        {
            keyNames.push_back({ keyCodes[i], keyboardLayoutMap[keyCodes[i]] });
        }
    }
    else
    {
        for (int i = 0; i < keyCodes.size(); i++)
        {
            keyNames.push_back({ keyCodes[i], keyboardLayoutMap[keyCodes[i]] });
        }
    }

    return keyNames;
}
