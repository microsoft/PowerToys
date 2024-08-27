#pragma once
#include "PropChangedEventArgs.g.h"

namespace winrt::Microsoft::Windows::CommandPalette::Extensions::implementation
{
    struct PropChangedEventArgs : PropChangedEventArgsT<PropChangedEventArgs>
    {
        PropChangedEventArgs() = default;
        PropChangedEventArgs(hstring propertyName) : PropertyName{ propertyName } {};

        til::property<hstring> PropertyName;
    };
}

namespace winrt::Microsoft::Windows::CommandPalette::Extensions::factory_implementation
{
    struct PropChangedEventArgs : PropChangedEventArgsT<PropChangedEventArgs, implementation::PropChangedEventArgs>
    {
    };
}
