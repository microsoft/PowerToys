#pragma once

#include "FancyZonesDataTypes.h"

namespace std
{
    template<>
    struct hash<GUID>
    {
        size_t operator()(const GUID& Value) const
        {
            RPC_STATUS status = RPC_S_OK;
            return ::UuidHash(&const_cast<GUID&>(Value), &status);
        }
    };

    template<>
    struct hash<FancyZonesDataTypes::DeviceIdData>
    {
        size_t operator()(const FancyZonesDataTypes::DeviceIdData& Value) const
        {
            // TODO(stefan): Implement this
            return 0;
        }
    };

}
