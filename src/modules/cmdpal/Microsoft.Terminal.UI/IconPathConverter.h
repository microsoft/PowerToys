#pragma once

#include "IconPathConverter.g.h"

namespace winrt::Microsoft::Terminal::UI::implementation
{
    struct IconPathConverter
    {
        IconPathConverter() = default;

        //static Windows::UI::Xaml::Controls::IconElement IconWUX(const winrt::hstring& iconPath);
        //static Windows::UI::Xaml::Controls::IconSource IconSourceWUX(const winrt::hstring& iconPath);
        static Microsoft::UI::Xaml::Controls::IconSource IconSourceMUX(const winrt::hstring& iconPath, bool convertToGrayscale, const int targetSize=24);
        static Microsoft::UI::Xaml::Controls::IconElement IconMUX(const winrt::hstring& iconPath);
        static Microsoft::UI::Xaml::Controls::IconElement IconMUX(const winrt::hstring& iconPath, const int targetSize);
    };
}

namespace winrt::Microsoft::Terminal::UI::factory_implementation
{
    BASIC_FACTORY(IconPathConverter);
}
