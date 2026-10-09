#pragma once
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <winrt/Windows.UI.ViewManagement.h>

struct WindowsColors
{
    using Color = winrt::Windows::UI::Color;

    static DWORD rgb_color(DWORD abgr_color);
    static DWORD rgb_color(Color color);
    static Color get_button_face_color();
    static Color get_button_text_color();
    static Color get_highlight_color();
    static Color get_hotlight_color();
    static Color get_highlight_text_color();
    static Color get_accent_light_1_color();
    static Color get_accent_light_2_color();
    static Color get_accent_dark_1_color();
    static Color get_accent_color();
    static Color get_background_color();
    static bool is_dark_mode();
    // Update colors - returns true if the values where changed
    bool update();

    DWORD accent_color_menu = 0,
          start_color_menu = 0,
          desktop_fill_color = 0;
    bool light_mode = true;
};
