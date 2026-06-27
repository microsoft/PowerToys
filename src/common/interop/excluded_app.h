#pragma once
#include <array>
#include <string>
#include <string_view>
#include <vector>

namespace shortcut_guide::interop
{
    inline constexpr std::array<std::wstring_view, 2> built_in_excluded_process_names = {
        L"AWP.EXE",
        L"ATLANTIS.EXE",
    };

    inline constexpr std::array<std::wstring_view, 1> built_in_excluded_window_titles = {
        L"ATLANTIS WORD PROCESSOR",
    };
}

extern "C"
{
    std::vector<std::wstring> m_excludedApps;
    __declspec(dllexport) bool IsCurrentWindowExcludedFromShortcutGuide();
}
