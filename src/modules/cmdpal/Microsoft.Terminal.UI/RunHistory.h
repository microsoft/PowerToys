#pragma once

#include "RunHistory.g.h"
#include "types.h"

namespace winrt::Microsoft::Terminal::UI::implementation
{
    struct RunHistory
    {
        RunHistory() = default;
        static winrt::Windows::Foundation::Collections::IVector<hstring> CreateRunHistory();

    private:
        winrt::Windows::Foundation::Collections::IVector<hstring> _mruHistory;
    };
}

namespace winrt::Microsoft::Terminal::UI::factory_implementation
{
    BASIC_FACTORY(RunHistory);
}
