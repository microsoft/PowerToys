#pragma once

namespace MeasurementLogic
{
    constexpr float DefaultDpi = 96.0f;

    enum Unit
    {
        Pixel = 1,
        Inch = 2,
        Centimetre = 4,
        Millimetre = 8,
        Dip = 16,
    };

    constexpr Unit GetUnitFromIndex(const int index) noexcept
    {
        switch (index)
        {
        case 0:
            return Unit::Pixel;
        case 1:
            return Unit::Inch;
        case 2:
            return Unit::Centimetre;
        case 3:
            return Unit::Millimetre;
        case 4:
            return Unit::Dip;
        default:
            return Unit::Pixel;
        }
    }

    constexpr float Convert(const float pixels, const Unit units, const float px2mmRatio, const float monitorDpi) noexcept
    {
        if (units == Unit::Pixel)
        {
            return pixels;
        }

        if (units == Unit::Dip)
        {
            return monitorDpi > 0 ? pixels * DefaultDpi / monitorDpi : pixels;
        }

        if (px2mmRatio > 0)
        {
            switch (units)
            {
            case Unit::Inch:
                return pixels * px2mmRatio / 25.4f;
            case Unit::Centimetre:
                return pixels * px2mmRatio / 10.0f;
            case Unit::Millimetre:
                return pixels * px2mmRatio;
            default:
                return pixels;
            }
        }

        switch (units)
        {
        case Unit::Inch:
            return pixels / DefaultDpi;
        case Unit::Centimetre:
            return pixels / DefaultDpi * 2.54f;
        case Unit::Millimetre:
            return pixels / DefaultDpi * 25.4f;
        default:
            return pixels;
        }
    }
}
