#pragma once

#include "ExplorerItemViewModel.g.h"

namespace winrt::PowerRenameUI::implementation
{
    struct ExplorerItemViewModel : ExplorerItemViewModelT<ExplorerItemViewModel>
    {
        ExplorerItemViewModel() = default;
        ExplorerItemViewModel(const uint32_t _index);

        int32_t IdVM();
        hstring IdStrVM();
        hstring OriginalVM();
        hstring RenamedVM();
        double IndentationVM();
        hstring ImagePathVM();
        int32_t TypeVM();
        bool CheckedVM();
        void CheckedVM(bool value);
        int32_t StateVM();

        winrt::event_token PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;

        uint32_t _index = 0;
        winrt::event<Microsoft::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;
    };
}

namespace winrt::PowerRenameUI::factory_implementation
{
    struct ExplorerItemViewModel : ExplorerItemViewModelT<ExplorerItemViewModel, implementation::ExplorerItemViewModel>
    {
    };
}
