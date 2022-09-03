#include "pch.h"

#include "Measurement.h"

Measurement::Measurement(RECT winRect)
{
    rect.left = static_cast<float>(winRect.left);
    rect.right = static_cast<float>(winRect.right);
    rect.top = static_cast<float>(winRect.top);
    rect.bottom = static_cast<float>(winRect.bottom);
}

Measurement::Measurement(D2D1_RECT_F d2dRect) :
    rect{ d2dRect }
{
}

namespace
{
    inline float Convert(const float pixels, const Measurement::Unit units)
    {
        switch (units)
        {
        case Measurement::Unit::Pixel:
            return pixels;
        case Measurement::Unit::Inch:
            return pixels / 96.f;
        case Measurement::Unit::Centimetre:
            return pixels / 96.f * 2.54f;
        default:
            return pixels;
        }
    }
}

inline float Measurement::Width(const Unit units) const
{
    return Convert(rect.right - rect.left + 1.f, units);
}

inline float Measurement::Height(const Unit units) const
{
    return Convert(rect.bottom - rect.top + 1.f, units);
}

Measurement::PrintResult Measurement::Print(wchar_t* buf,
                                            const size_t bufSize,
                                            const bool printWidth,
                                            const bool printHeight,
                                            const Unit units) const
{
    PrintResult result;
    if (printWidth)
    {
        result.strLen += swprintf_s(buf,
                                    bufSize,
                                    L"%g",
                                    Width(units));
        if (printHeight)
        {
            result.crossSymbolPos = result.strLen + 1;
            result.strLen += swprintf_s(buf + result.strLen,
                                        bufSize - result.strLen,
                                        L" \x00D7 ");
        }
    }

    if (printHeight)
    {
        result.strLen += swprintf_s(buf + result.strLen,
                                    bufSize - result.strLen,
                                    L"%g",
                                    Height(units));
    }

    switch (units)
    {
    case Measurement::Unit::Inch:
        result.strLen += swprintf_s(buf + result.strLen,
                                    bufSize - result.strLen,
                                    L" in");
        break;
    case Measurement::Unit::Centimetre:
        result.strLen += swprintf_s(buf + result.strLen,
                                    bufSize - result.strLen,
                                    L" cm");
        break;
    }

    return result;
}
