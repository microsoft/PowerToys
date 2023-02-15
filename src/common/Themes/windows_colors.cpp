#include "windows_colors.h"
#include "theme_helpers.h"

DWORD WindowsColors::rgb_color(DWORD abgr_color)
{
    // registry keeps the colors in ABGR format, we want RGB
    auto r = (abgr_color & 0xFF);
    auto g = (abgr_color & 0xFF00) >> 8;
    auto b = (abgr_color & 0xFF0000) >> 16;
    return (r << 16) | (g << 8) | b;
}
DWORD WindowsColors::rgb_color(winrt::Windows::UI::Color color)
{
    return static_cast<DWORD>((color.R << 16) | (color.G << 8) | (color.B));
}
WindowsColors::Color WindowsColors::get_button_face_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.UIElementColor(winrt::Windows::UI::ViewManagement::UIElementType::ButtonFace);
}
WindowsColors::Color WindowsColors::get_button_text_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.UIElementColor(winrt::Windows::UI::ViewManagement::UIElementType::ButtonText);
}
WindowsColors::Color WindowsColors::get_highlight_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.UIElementColor(winrt::Windows::UI::ViewManagement::UIElementType::Highlight);
}
WindowsColors::Color WindowsColors::get_hotlight_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.UIElementColor(winrt::Windows::UI::ViewManagement::UIElementType::Hotlight);
}
WindowsColors::Color WindowsColors::get_highlight_text_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.UIElementColor(winrt::Windows::UI::ViewManagement::UIElementType::HighlightText);
}
WindowsColors::Color WindowsColors::get_accent_light_1_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.GetColorValue(winrt::Windows::UI::ViewManagement::UIColorType::AccentLight1);
}
WindowsColors::Color WindowsColors::get_accent_light_2_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.GetColorValue(winrt::Windows::UI::ViewManagement::UIColorType::AccentLight2);
}
WindowsColors::Color WindowsColors::get_accent_dark_1_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.GetColorValue(winrt::Windows::UI::ViewManagement::UIColorType::AccentDark1);
}
WindowsColors::Color WindowsColors::get_accent_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.GetColorValue(winrt::Windows::UI::ViewManagement::UIColorType::Accent);
}
WindowsColors::Color WindowsColors::get_background_color()
{
    winrt::Windows::UI::ViewManagement::UISettings uiSettings;
    return uiSettings.GetColorValue(winrt::Windows::UI::ViewManagement::UIColorType::Background);
}

bool WindowsColors::is_dark_mode()
{
    return ThemeHelpers::GetAppTheme() == AppTheme::Dark;
}

bool WindowsColors::update()
{
    auto new_accent_color_menu = rgb_color(get_accent_color());
    auto new_start_color_menu = new_accent_color_menu;
    auto new_desktop_fill_color = rgb_color(GetSysColor(COLOR_DESKTOP));
    auto new_light_mode = rgb_color(get_background_color()) != 0; //Dark mode will have black as the background color.

    bool changed = new_accent_color_menu != accent_color_menu ||
                   new_start_color_menu != start_color_menu ||
                   new_light_mode != light_mode ||
                   new_desktop_fill_color != desktop_fill_color;
    accent_color_menu = new_accent_color_menu;
    start_color_menu = new_start_color_menu;
    light_mode = new_light_mode;
    desktop_fill_color = new_desktop_fill_color;

    return changed;
}
