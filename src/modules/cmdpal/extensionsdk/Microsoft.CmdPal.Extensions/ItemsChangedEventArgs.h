#pragma once
#include "ItemsChangedEventArgs.g.h"

namespace winrt::Microsoft::CmdPal::Extensions::implementation
{
    struct ItemsChangedEventArgs : ItemsChangedEventArgsT<ItemsChangedEventArgs>
    {
        ItemsChangedEventArgs() = default;
        ItemsChangedEventArgs(int32_t total) : TotalItems{ total } {};

        til::property<int32_t> TotalItems;
    };
}

namespace winrt::Microsoft::CmdPal::Extensions::factory_implementation
{
    struct ItemsChangedEventArgs : ItemsChangedEventArgsT<ItemsChangedEventArgs, implementation::ItemsChangedEventArgs>
    {
    };
}
