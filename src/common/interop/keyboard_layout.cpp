#include "pch.h"
#include <array>
#include <algorithm>

#include "keyboard_layout_impl.h"
#include "shared_constants.h"
#include "GetLocalisation.h"

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
    const int result = ToUnicodeEx(vCode, scanCode, keyState, outBuffer.data(), (int)outBuffer.size(), wFlags, layout);
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
    keyboardLayoutMap[VK_CANCEL] = GetLocalisation(IDS_KEYBOARD_BREAK);
    keyboardLayoutMap[VK_BACK] = GetLocalisation(IDS_KEYBOARD_BACKSPACE);
    keyboardLayoutMap[VK_TAB] = GetLocalisation(IDS_KEYBOARD_TAB);
    keyboardLayoutMap[VK_CLEAR] = GetLocalisation(IDS_KEYBOARD_CLEAR);
    keyboardLayoutMap[VK_RETURN] = GetLocalisation(IDS_KEYBOARD_ENTER);
    keyboardLayoutMap[VK_SHIFT] = GetLocalisation(IDS_KEYBOARD_SHIFT);
    keyboardLayoutMap[VK_CONTROL] = GetLocalisation(IDS_KEYBOARD_CTRL);
    keyboardLayoutMap[VK_MENU] = GetLocalisation(IDS_KEYBOARD_ALT);
    keyboardLayoutMap[VK_PAUSE] = GetLocalisation(IDS_KEYBOARD_PAUSE);
    keyboardLayoutMap[VK_CAPITAL] = GetLocalisation(IDS_KEYBOARD_CAPS_LOCK);
    keyboardLayoutMap[VK_ESCAPE] = GetLocalisation(IDS_KEYBOARD_ESC);
    keyboardLayoutMap[VK_SPACE] = GetLocalisation(IDS_KEYBOARD_SPACE);
    keyboardLayoutMap[VK_PRIOR] = GetLocalisation(IDS_KEYBOARD_PGUP);
    keyboardLayoutMap[VK_NEXT] = GetLocalisation(IDS_KEYBOARD_PGDN);
    keyboardLayoutMap[VK_END] = GetLocalisation(IDS_KEYBOARD_END);
    keyboardLayoutMap[VK_HOME] = GetLocalisation(IDS_KEYBOARD_HOME);
    keyboardLayoutMap[VK_LEFT] = GetLocalisation(IDS_KEYBOARD_LEFT);
    keyboardLayoutMap[VK_UP] = GetLocalisation(IDS_KEYBOARD_UP);
    keyboardLayoutMap[VK_RIGHT] = GetLocalisation(IDS_KEYBOARD_RIGHT);
    keyboardLayoutMap[VK_DOWN] = GetLocalisation(IDS_KEYBOARD_DOWN);
    keyboardLayoutMap[VK_SELECT] = GetLocalisation(IDS_KEYBOARD_SELECT);
    keyboardLayoutMap[VK_PRINT] = GetLocalisation(IDS_KEYBOARD_PRINT);
    keyboardLayoutMap[VK_EXECUTE] = GetLocalisation(IDS_KEYBOARD_EXECUTE);
    keyboardLayoutMap[VK_SNAPSHOT] = GetLocalisation(IDS_KEYBOARD_PRINT_SCREEN);
    keyboardLayoutMap[VK_INSERT] = GetLocalisation(IDS_KEYBOARD_INSERT);
    keyboardLayoutMap[VK_DELETE] = GetLocalisation(IDS_KEYBOARD_DELETE);
    keyboardLayoutMap[VK_HELP] = GetLocalisation(IDS_KEYBOARD_HELP);
    keyboardLayoutMap[VK_LWIN] = GetLocalisation(IDS_KEYBOARD_WIN_LEFT);
    keyboardLayoutMap[VK_RWIN] = GetLocalisation(IDS_KEYBOARD_WIN_RIGHT);
    keyboardLayoutMap[VK_APPS] = GetLocalisation(IDS_KEYBOARD_APPS_MENU);
    keyboardLayoutMap[VK_SLEEP] = GetLocalisation(IDS_KEYBOARD_SLEEP);
    keyboardLayoutMap[VK_NUMPAD0] = GetLocalisation(IDS_KEYBOARD_NUMPAD0);
    keyboardLayoutMap[VK_NUMPAD1] = GetLocalisation(IDS_KEYBOARD_NUMPAD1);
    keyboardLayoutMap[VK_NUMPAD2] = GetLocalisation(IDS_KEYBOARD_NUMPAD2);
    keyboardLayoutMap[VK_NUMPAD3] = GetLocalisation(IDS_KEYBOARD_NUMPAD3);
    keyboardLayoutMap[VK_NUMPAD4] = GetLocalisation(IDS_KEYBOARD_NUMPAD4);
    keyboardLayoutMap[VK_NUMPAD5] = GetLocalisation(IDS_KEYBOARD_NUMPAD5);
    keyboardLayoutMap[VK_NUMPAD6] = GetLocalisation(IDS_KEYBOARD_NUMPAD6);
    keyboardLayoutMap[VK_NUMPAD7] = GetLocalisation(IDS_KEYBOARD_NUMPAD7);
    keyboardLayoutMap[VK_NUMPAD8] = GetLocalisation(IDS_KEYBOARD_NUMPAD8);
    keyboardLayoutMap[VK_NUMPAD9] = GetLocalisation(IDS_KEYBOARD_NUMPAD9);
    keyboardLayoutMap[VK_SEPARATOR] = GetLocalisation(IDS_KEYBOARD_SEPARATOR);
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
    keyboardLayoutMap[VK_NUMLOCK] = GetLocalisation(IDS_KEYBOARD_NUM_LOCK);
    keyboardLayoutMap[VK_SCROLL] = GetLocalisation(IDS_KEYBOARD_SCROLL_LOCK);
    keyboardLayoutMap[VK_LSHIFT] = GetLocalisation(IDS_KEYBOARD_SHIFT_LEFT);
    keyboardLayoutMap[VK_RSHIFT] = GetLocalisation(IDS_KEYBOARD_SHIFT_RIGHT);
    keyboardLayoutMap[VK_LCONTROL] = GetLocalisation(IDS_KEYBOARD_CTRL_LEFT);
    keyboardLayoutMap[VK_RCONTROL] = GetLocalisation(IDS_KEYBOARD_CTRL_RIGHT);
    keyboardLayoutMap[VK_LMENU] = GetLocalisation(IDS_KEYBOARD_ALT_LEFT);
    keyboardLayoutMap[VK_RMENU] = GetLocalisation(IDS_KEYBOARD_ALT_RIGHT);
    keyboardLayoutMap[VK_BROWSER_BACK] = GetLocalisation(IDS_KEYBOARD_BROWSER_BACK);
    keyboardLayoutMap[VK_BROWSER_FORWARD] = GetLocalisation(IDS_KEYBOARD_BROWSER_FORWARD);
    keyboardLayoutMap[VK_BROWSER_REFRESH] = GetLocalisation(IDS_KEYBOARD_BROWSER_REFRESH);
    keyboardLayoutMap[VK_BROWSER_STOP] = GetLocalisation(IDS_KEYBOARD_BROWSER_STOP);
    keyboardLayoutMap[VK_BROWSER_SEARCH] = GetLocalisation(IDS_KEYBOARD_BROWSER_SEARCH);
    keyboardLayoutMap[VK_BROWSER_FAVORITES] = GetLocalisation(IDS_KEYBOARD_BROWSER_FAVORITES);
    keyboardLayoutMap[VK_BROWSER_HOME] = GetLocalisation(IDS_KEYBOARD_BROWSER_HOME);
    keyboardLayoutMap[VK_VOLUME_MUTE] = GetLocalisation(IDS_KEYBOARD_VOLUME_MUTE);
    keyboardLayoutMap[VK_VOLUME_DOWN] = GetLocalisation(IDS_KEYBOARD_VOLUME_DOWN);
    keyboardLayoutMap[VK_VOLUME_UP] = GetLocalisation(IDS_KEYBOARD_VOLUME_UP);
    keyboardLayoutMap[VK_MEDIA_NEXT_TRACK] = GetLocalisation(IDS_KEYBOARD_NEXT_TRACK);
    keyboardLayoutMap[VK_MEDIA_PREV_TRACK] = GetLocalisation(IDS_KEYBOARD_PREVIOUS_TRACK);
    keyboardLayoutMap[VK_MEDIA_STOP] = GetLocalisation(IDS_KEYBOARD_STOP_MEDIA);
    keyboardLayoutMap[VK_MEDIA_PLAY_PAUSE] = GetLocalisation(IDS_KEYBOARD_PLAY_PAUSE_MEDIA);
    keyboardLayoutMap[VK_LAUNCH_MAIL] = GetLocalisation(IDS_KEYBOARD_START_MAIL);
    keyboardLayoutMap[VK_LAUNCH_MEDIA_SELECT] = GetLocalisation(IDS_KEYBOARD_SELECT_MEDIA);
    keyboardLayoutMap[VK_LAUNCH_APP1] = GetLocalisation(IDS_KEYBOARD_START_APP1);
    keyboardLayoutMap[VK_LAUNCH_APP2] = GetLocalisation(IDS_KEYBOARD_START_APP2);
    keyboardLayoutMap[VK_PACKET] = L"Packet";
    keyboardLayoutMap[VK_ATTN] = GetLocalisation(IDS_KEYBOARD_ATTN);
    keyboardLayoutMap[VK_CRSEL] = L"CrSel";
    keyboardLayoutMap[VK_EXSEL] = L"ExSel";
    keyboardLayoutMap[VK_EREOF] = GetLocalisation(IDS_KEYBOARD_ERASE_EOF);
    keyboardLayoutMap[VK_PLAY] = GetLocalisation(IDS_KEYBOARD_PLAY);
    keyboardLayoutMap[VK_ZOOM] = GetLocalisation(IDS_KEYBOARD_ZOOM);
    keyboardLayoutMap[VK_PA1] = L"PA1";
    keyboardLayoutMap[VK_OEM_CLEAR] = GetLocalisation(IDS_KEYBOARD_CLEAR);
    keyboardLayoutMap[0xFF] = GetLocalisation(IDS_KEYBOARD_UNDEFINED);
    keyboardLayoutMap[CommonSharedConstants::VK_WIN_BOTH] = L"Win";
    keyboardLayoutMap[VK_KANA] = L"IME Kana";
    keyboardLayoutMap[VK_HANGEUL] = L"IME Hangeul";
    keyboardLayoutMap[VK_HANGUL] = L"IME Hangul";
    keyboardLayoutMap[VK_JUNJA] = L"IME Junja";
    keyboardLayoutMap[VK_FINAL] = L"IME Final";
    keyboardLayoutMap[VK_HANJA] = L"IME Hanja";
    keyboardLayoutMap[VK_KANJI] = L"IME Kanji";
    keyboardLayoutMap[VK_CONVERT] = GetLocalisation(IDS_KEYBOARD_IME_CONVERT);
    keyboardLayoutMap[VK_NONCONVERT] = GetLocalisation(IDS_KEYBOARD_IME_NON_CONVERT);
    keyboardLayoutMap[VK_ACCEPT] = L"IME Kana";
    keyboardLayoutMap[VK_MODECHANGE] = GetLocalisation(IDS_KEYBOARD_IME_MODE_CHANGE);
    keyboardLayoutMap[CommonSharedConstants::VK_DISABLED] = GetLocalisation(IDS_KEYBOARD_DISABLE);
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
