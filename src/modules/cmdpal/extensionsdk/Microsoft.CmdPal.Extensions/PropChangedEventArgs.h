#pragma once
#include "PropChangedEventArgs.g.h"

namespace winrt::Microsoft::CmdPal::Extensions::implementation
{
    struct PropChangedEventArgs : PropChangedEventArgsT<PropChangedEventArgs>
    {
        PropChangedEventArgs() = default;
        PropChangedEventArgs(hstring propertyName) : PropertyName{ propertyName } {};

        til::property<hstring> PropertyName;
    };
}

namespace winrt::Microsoft::CmdPal::Extensions::factory_implementation
{
    struct PropChangedEventArgs : PropChangedEventArgsT<PropChangedEventArgs, implementation::PropChangedEventArgs>
    {
    };
}
