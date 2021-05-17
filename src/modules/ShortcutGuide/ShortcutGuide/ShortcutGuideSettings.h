#pragma once
#include <string>

struct ShortcutGuideSettings
{
    std::wstring hotkey = L"shift+win+/";
    int overlayOpacity = 90;
    std::wstring theme = L"system";
    std::wstring disabledApps = L"";
};
