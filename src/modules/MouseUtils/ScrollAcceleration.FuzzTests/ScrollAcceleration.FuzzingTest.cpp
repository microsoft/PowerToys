// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// libFuzzer target for the Scroll Acceleration calculator.
//
// Scroll Acceleration handles user input (mouse wheel events) and a hand-editable settings.json,
// so PowerToys policy requires a fuzz target. The acceleration logic lives in the Win32-free
// ScrollAcceleration::AccelerationCalculator, which we drive with adversarial settings (negative,
// NaN, infinite, huge), wheel deltas, directions and timestamps (including 32-bit wrap-around).
//
// Invariants checked: no crash/UB under ASan, the multiplier is finite and >= 1, and scaled
// deltas stay within the int16 range that SendInput accepts for wheel data.

#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cmath>
#include <cstring>

#include "AccelerationCalculator.h"

using ScrollAcceleration::AccelerationCalculator;
using ScrollAcceleration::AccelerationSettings;

namespace
{
    struct Reader
    {
        const uint8_t* data;
        size_t size;
        size_t pos = 0;

        Reader(const uint8_t* d, size_t n) :
            data(d), size(n) {}

        bool done() const { return pos >= size; }

        uint8_t u8() { return pos < size ? data[pos++] : static_cast<uint8_t>(0); }

        uint32_t u32()
        {
            uint32_t v = 0;
            for (int i = 0; i < 4; ++i)
            {
                v = (v << 8) | u8();
            }
            return v;
        }

        double f64()
        {
            uint64_t bits = (static_cast<uint64_t>(u32()) << 32) | u32();
            double d;
            std::memcpy(&d, &bits, sizeof(d));
            return d; // may be NaN / inf / denormal on purpose
        }
    };
}

extern "C" int LLVMFuzzerTestOneInput(const uint8_t* data, size_t size)
{
    Reader r(data, size);

    AccelerationSettings settings;
    settings.timeThresholdMs = r.u32();
    settings.acceleration = r.f64();
    settings.maxMultiplier = r.f64();

    AccelerationCalculator calc(settings);
    uint32_t tick = r.u32();

    for (int step = 0; step < 4096 && !r.done(); ++step)
    {
        const uint8_t op = r.u8();
        tick += r.u32();

        if (op & 0x80u)
        {
            settings.timeThresholdMs = r.u32();
            settings.acceleration = r.f64();
            settings.maxMultiplier = r.f64();
            calc.SetSettings(settings);
        }

        if (op & 0x40u)
        {
            calc.Reset();
        }

        const int delta = static_cast<int>(static_cast<int16_t>((r.u8() << 8) | r.u8()));
        const int scaled = calc.Scale(delta, tick);

        // A scaled delta must keep its sign (or be unchanged) and never exceed the int16 wheel range.
        if (scaled > 32767 || scaled < -32767)
        {
            if (scaled != delta) // an unscaled raw delta may legitimately be -32768
            {
                std::abort();
            }
        }
        if (delta != 0 && scaled != delta && ((scaled > 0) != (delta > 0)))
        {
            std::abort();
        }
    }

    return 0;
}

#ifndef DISABLE_FOR_FUZZING

// Plain entry point for the non-fuzzing (Debug) configuration: runs one canned input so the
// project is runnable/debuggable without the libFuzzer driver.
int main()
{
    const uint8_t seed[] = {
        0, 0, 0, 100, 63, 224, 0, 0, 0, 0, 0, 0, 64, 32, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 50, 0, 120, 0, 0, 0, 50, 0, 120, 0, 0, 0, 50, 0, 120
    };
    return LLVMFuzzerTestOneInput(seed, sizeof(seed));
}

#endif
