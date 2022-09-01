#pragma once

#include <dcommon.h>
#include <windef.h>

struct Measurement
{
    enum Unit
    {
        Pixel,
        Inch,
        Centimetre
    };

    D2D1_RECT_F rect = {}; // corners are inclusive

    Measurement() = default;
    Measurement(const Measurement&) = default;
    Measurement& operator=(const Measurement&) = default;

    explicit Measurement(D2D1_RECT_F d2dRect);
    explicit Measurement(RECT winRect);

    float Width(const Unit units) const;
    float Height(const Unit units) const;

    struct PrintResult
    {
        std::optional<size_t> crossSymbolPos;
        size_t strLen = {};
    };

    PrintResult Print(wchar_t* buf,
                      const size_t bufSize,
                      const bool printWidth,
                      const bool printHeight,
                      const Unit units) const;
};
