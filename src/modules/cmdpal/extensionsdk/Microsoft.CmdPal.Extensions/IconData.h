#pragma once
#include "IconData.g.h"
#include "IconInfo.g.h"

namespace winrt::Microsoft::CmdPal::Extensions::implementation
{
    struct IconData : IconDataT<IconData>
    {
        IconData(hstring iconPath) :
            Icon(iconPath){};

        IconData(Windows::Storage::Streams::IRandomAccessStreamReference iconData) :
            Data(iconData){};

        static Microsoft::CmdPal::Extensions::IconData FromStream(Windows::Storage::Streams::IRandomAccessStreamReference stream)
        {
            return *winrt::make_self<IconData>(stream);
        }
        
        til::property<hstring> Icon;
        til::property<Windows::Storage::Streams::IRandomAccessStreamReference> Data;
    };
}
namespace winrt::Microsoft::CmdPal::Extensions::factory_implementation
{
    struct IconData : IconDataT<IconData, implementation::IconData>
    {
    };
}

namespace winrt::Microsoft::CmdPal::Extensions::implementation
{
    struct IconInfo : IconInfoT<IconInfo>
    {
        IconInfo(hstring iconPath) :
            Light(iconPath),
            Dark(iconPath){};

        IconInfo(Extensions::IconData light, Extensions::IconData dark) :
            Light(light),
            Dark(dark) {};
        
        til::property<Extensions::IconData> Light;
        til::property<Extensions::IconData> Dark;       
    };
}
namespace winrt::Microsoft::CmdPal::Extensions::factory_implementation
{
    struct IconInfo : IconInfoT<IconInfo, implementation::IconInfo>
    {
    };
}
