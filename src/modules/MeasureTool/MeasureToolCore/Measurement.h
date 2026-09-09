#pragma once
#include "pch.h"

#include <dcommon.h>
#include <windef.h>
#include <iosfwd>

#include "MeasurementLogic.h"

struct Measurement
{
    using Unit = MeasurementLogic::Unit;

    D2D1_RECT_F rect = {}; // corners are inclusive

    float px2mmRatio = 0;
    float monitorDpi = MeasurementLogic::DefaultDpi;
    static winrt::hstring abbreviations[5]; // Abbreviations of units.

    Measurement(const Measurement&) = default;
    Measurement& operator=(const Measurement&) = default;

    explicit Measurement(D2D1_RECT_F d2dRect, float px2mmRatio, float monitorDpi);
    explicit Measurement(RECT winRect, float px2mmRatio, float monitorDpi);

    float Width(const Unit units) const;
    float Height(const Unit units) const;

    struct PrintResult
    {
        size_t crossSymbolPos[2] = {};
        size_t strLen = {};
    };

    static void InitResources();
    static constexpr Unit GetUnitFromIndex(const int index) noexcept
    {
        return MeasurementLogic::GetUnitFromIndex(index);
    }

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
};
