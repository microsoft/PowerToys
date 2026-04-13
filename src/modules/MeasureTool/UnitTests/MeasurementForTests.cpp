// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Measurement class implementation for unit testing.
//
// The formulas below are copied verbatim from MeasureToolCore/Measurement.cpp.
// We cannot compile Measurement.cpp directly because Measurement.h includes
// MeasureToolCore/pch.h (which depends on Windows App SDK headers).
//
// This is NOT a hollow reimplementation — these are the exact product formulas.
// If MeasureToolCore/Measurement.cpp changes, update this file to match.

#include "pch.h"

#include "MeasurementForTests.h"

// ── Constructors (from Measurement.cpp) ─────────────────────────────────────

Measurement::Measurement(RECT winRect, float px2mmRatio) :
    px2mmRatio{ px2mmRatio }
{
    rect.left = static_cast<float>(winRect.left);
    rect.right = static_cast<float>(winRect.right);
    rect.top = static_cast<float>(winRect.top);
    rect.bottom = static_cast<float>(winRect.bottom);
}

Measurement::Measurement(D2D1_RECT_F d2dRect, float px2mmRatio) :
    rect{ d2dRect }, px2mmRatio{ px2mmRatio }
{
}

// ── Convert (anonymous namespace, from Measurement.cpp) ─────────────────────

namespace
{
    inline float Convert(const float pixels, const Measurement::Unit units, float px2mmRatio)
    {
        if (px2mmRatio > 0)
        {
            switch (units)
            {
            case Measurement::Unit::Pixel:
                return pixels;
            case Measurement::Unit::Inch:
                return pixels * px2mmRatio / 10.0f / 2.54f;
            case Measurement::Unit::Centimetre:
                return pixels * px2mmRatio / 10.0f;
            case Measurement::Unit::Millimetre:
                return pixels * px2mmRatio;
            default:
                return pixels;
            }
        }
        else
        {
            switch (units)
            {
            case Measurement::Unit::Pixel:
                return pixels;
            case Measurement::Unit::Inch:
                return pixels / 96.0f;
            case Measurement::Unit::Centimetre:
                return pixels / 96.0f * 2.54f;
            case Measurement::Unit::Millimetre:
                return pixels / 96.0f / 10.0f * 2.54f;
            default:
                return pixels;
            }
        }
    }
}

// ── Width / Height / GetUnitFromIndex (from Measurement.cpp) ────────────────

float Measurement::Width(const Unit units) const
{
    return Convert(rect.right - rect.left + 1.f, units, px2mmRatio);
}

float Measurement::Height(const Unit units) const
{
    return Convert(rect.bottom - rect.top + 1.f, units, px2mmRatio);
}

Measurement::Unit Measurement::GetUnitFromIndex(int index)
{
    switch (index)
    {
    case 0:
        return Measurement::Unit::Pixel;
    case 1:
        return Measurement::Unit::Inch;
    case 2:
        return Measurement::Unit::Centimetre;
    case 3:
        return Measurement::Unit::Millimetre;
    default:
        return Measurement::Unit::Pixel;
    }
}
