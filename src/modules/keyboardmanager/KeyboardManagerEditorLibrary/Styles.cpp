#include "pch.h"
#include "Styles.h"
#include <common/themes/windows_colors.h>

Style AccentButtonStyle()
{
    Style style{ winrt::xaml_typename<Controls::Button>() };
    style.Setters().Append(Setter{
        Controls::Control::BackgroundProperty(),
        winrt::Windows::UI::Xaml::Media::SolidColorBrush{ WindowsColors::get_accent_color() } });
    style.Setters().Append(Setter{
        Controls::Control::ForegroundProperty(),
        winrt::Windows::UI::Xaml::Media::SolidColorBrush{ winrt::Windows::UI::Colors::White() } });
    return style;
}