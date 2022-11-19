#pragma once
#include <string>

struct ShortcutGuideSettings
{
    std::wstring hotkey = L"shift+win+/";
    int overlayOpacity = 90;
    std::wstring theme = L"system";
    std::wstring disabledApps = L"";
    bool shouldReactToPressedWinKey = false;
    int windowsKeyPressTimeForGlobalWindowsShortcuts = 900;
    int windowsKeyPressTimeForTaskbarIconShortcuts = 900;
};
