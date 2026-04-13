#pragma once
#include "pch.h"

#include <dcommon.h>
#include <windef.h>
#include <iosfwd>

struct Measurement
{
    enum Unit
    {
        Pixel = 1,
        Inch = 2,
        Centimetre = 4,
        Millimetre = 8,
    };

    D2D1_RECT_F rect = {}; // corners are inclusive

    float px2mmRatio = 0;
#ifndef UNIT_TEST
    static winrt::hstring abbreviations[4]; // Abbreviations of units.
#endif

    Measurement(const Measurement&) = default;
    Measurement& operator=(const Measurement&) = default;

    explicit Measurement(D2D1_RECT_F d2dRect, float px2mmRatio);
    explicit Measurement(RECT winRect, float px2mmRatio);

    float Width(const Unit units) const;
    float Height(const Unit units) const;

    static Unit GetUnitFromIndex(int index);

#ifndef UNIT_TEST
    struct PrintResult
    {
        size_t crossSymbolPos[2] = {};
        size_t strLen = {};
    };

    static void InitResources();
    static const wchar_t* GetUnitAbbreviation(const Unit units);

    PrintResult Print(wchar_t* buf,
                      const size_t bufSize,
                      const bool printWidth,
                      const bool printHeight,
                      const int units) const;

    void PrintToStream(std::wostream& stream,
                       const bool prependNewLine,
                       const bool printWidth,
                       const bool printHeight,
                       const Unit units) const;
#endif // !UNIT_TEST
};
