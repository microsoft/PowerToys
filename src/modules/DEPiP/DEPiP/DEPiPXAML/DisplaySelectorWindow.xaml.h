#pragma once

#include "../DisplayCapture.h"
#include "DisplaySelectorWindow.g.h"

namespace winrt::DEPiP::implementation
{
    struct DisplaySelectorWindow : DisplaySelectorWindowT<DisplaySelectorWindow>
    {
        DisplaySelectorWindow();
        void Initialize(std::vector<DisplayInfo> displays, std::function<void(DisplayInfo const&)> selected);
        void Cancel_Click(
            Windows::Foundation::IInspectable const&,
            Microsoft::UI::Xaml::RoutedEventArgs const&);

    private:
        std::vector<DisplayInfo> m_displays;
        std::function<void(DisplayInfo const&)> m_selected;
    };
}

namespace winrt::DEPiP::factory_implementation
{
    struct DisplaySelectorWindow : DisplaySelectorWindowT<DisplaySelectorWindow, implementation::DisplaySelectorWindow>
    {
    };
}
