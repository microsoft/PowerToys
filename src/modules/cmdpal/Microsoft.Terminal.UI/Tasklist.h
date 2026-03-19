#pragma once

#include "Tasklist.g.h"

namespace winrt::Microsoft::Terminal::UI::implementation
{
    struct Tasklist
    {
        Tasklist() = default;
        static winrt::Windows::Foundation::Collections::IVector<winrt::Microsoft::Terminal::UI::TasklistButton> GetButtons();
    };
}

namespace winrt::Microsoft::Terminal::UI::factory_implementation
{
    BASIC_FACTORY(Tasklist);
}
