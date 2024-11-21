#pragma once
#include "IconDataType.g.h"

namespace winrt::Microsoft::CmdPal::Extensions::implementation
{
    struct IconDataType : IconDataTypeT<IconDataType>
    {
        IconDataType(hstring iconPath) :
            Icon(iconPath){};

        IconDataType(Windows::Storage::Streams::IRandomAccessStreamReference iconData) :
            Data(iconData){};

        static Microsoft::CmdPal::Extensions::IconDataType FromStream(Windows::Storage::Streams::IRandomAccessStreamReference stream)
        {
            return *winrt::make_self<IconDataType>(stream);
        }
        
        til::property<hstring> Icon;
        til::property<Windows::Storage::Streams::IRandomAccessStreamReference> Data;
        

    };
}
namespace winrt::Microsoft::CmdPal::Extensions::factory_implementation
{
    struct IconDataType : IconDataTypeT<IconDataType, implementation::IconDataType>
    {
    };
}
