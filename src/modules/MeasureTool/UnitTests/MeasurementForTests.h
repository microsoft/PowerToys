// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

// Mirror of MeasureToolCore/Measurement.h for test compilation.
// The real Measurement.h includes MeasureToolCore/pch.h which pulls in
// Windows App SDK / WinUI headers not available in the test project.
// This header provides the same struct declaration with only the
// lightweight dependencies already in the test project's pch.h.
//
// KEEP IN SYNC with MeasureToolCore/Measurement.h when the struct changes.

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

    Measurement(const Measurement&) = default;
    Measurement& operator=(const Measurement&) = default;

    explicit Measurement(D2D1_RECT_F d2dRect, float px2mmRatio);
    explicit Measurement(RECT winRect, float px2mmRatio);

    float Width(const Unit units) const;
    float Height(const Unit units) const;

    static Unit GetUnitFromIndex(int index);
};
