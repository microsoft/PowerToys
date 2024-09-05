#pragma once
#include "IconDataType.g.h"

namespace winrt::Microsoft::CmdPal::Extensions::implementation
{
    struct IconDataType : IconDataTypeT<IconDataType>
    {
        IconDataType(hstring iconPath) :
            Icon(iconPath)
        {

        };

        til::property<hstring> Icon;
    };
}
namespace winrt::Microsoft::CmdPal::Extensions::factory_implementation
{
    struct IconDataType : IconDataTypeT<IconDataType, implementation::IconDataType>
    {
    };
}
