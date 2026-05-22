#pragma once

extern "C"
{
    std::vector<std::wstring> m_excludedApps;
    __declspec(dllexport) bool IsCurrentWindowExcludedFromShortcutGuide();
}
