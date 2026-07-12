// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// libFuzzer target for the Mouse Button Lock engine state machine.
//
// Mouse Button Lock is a user-input module, so PowerToys policy requires a fuzz target
// (AGENTS.md: "New modules handling file I/O or user input must implement fuzzing tests").
// The decision logic lives in the Win32-free mousebuttonlock::Engine (MouseButtonLockCore.h),
// so we can drive it deterministically under ASan with no OS state involved: the fuzzer's
// bytes are decoded into a sequence of engine events (button down/up, cursor move, settings
// changes, and lifecycle calls) with adversarial ticks, coordinates, and settings. A counting
// injector stands in for the real SendInput-based IButtonUpInjector.
//
// The goal is to prove the state machine stays memory-safe and crash-free for arbitrary input,
// including extreme coordinates and hold/dead-zone values a hand-edited settings.json could
// produce. (Writing this target is what surfaced the squared-distance overflow now fixed in
// CheckMoveCancel.)

#include <cstddef>
#include <cstdint>

#include "MouseButtonLockCore.h"

using namespace mousebuttonlock;

namespace
{
    // Records release-injection calls and can be told to fail, exercising the tap-to-release
    // injection-failure path in the engine.
    class CountingInjector : public IButtonUpInjector
    {
    public:
        int count = 0;
        bool succeed = true;

        bool InjectUp(MouseButton) override
        {
            ++count;
            return succeed;
        }
    };

    // Bounds-checked reader over the fuzzer buffer: reads past the end yield 0 so the harness
    // never reads out of range (ASan would flag it otherwise).
    struct Reader
    {
        const uint8_t* data;
        size_t size;
        size_t pos = 0;

        explicit Reader(const uint8_t* d, size_t n) :
            data(d), size(n) {}

        bool done() const { return pos >= size; }

        uint8_t u8()
        {
            return pos < size ? data[pos++] : static_cast<uint8_t>(0);
        }

        uint32_t u32()
        {
            uint32_t v = 0;
            for (int i = 0; i < 4; ++i)
            {
                v = (v << 8) | u8();
            }
            return v;
        }
    };
}

extern "C" int LLVMFuzzerTestOneInput(const uint8_t* data, size_t size)
{
    Reader r(data, size);

    CountingInjector injector;
    Engine engine(injector);

    Settings settings; // shipping defaults; mutated below from fuzzer bytes
    uint64_t tick = 0;

    // Seed whether synthetic-up injection "succeeds", to reach both tap-to-release branches.
    injector.succeed = (r.u8() & 1u) != 0u;

    // Bound the number of ops so any single input terminates regardless of size.
    for (int step = 0; step < 4096 && !r.done(); ++step)
    {
        const uint8_t op = r.u8();

        // Advance the monotonic-ish clock by a fuzzer-chosen delta. Covers 0, small, and large
        // jumps; deltas can also push tick past a later downTick to exercise unsigned wrap.
        tick += r.u32();

        // Periodically mutate the settings snapshot, including out-of-range / negative values
        // that a hand-edited settings.json could carry (the engine must stay defined for them).
        if (op & 0x40u)
        {
            const uint8_t flags = r.u8();
            settings.rmbEnabled = (flags & 0x01u) != 0u;
            settings.mmbEnabled = (flags & 0x02u) != 0u;
            settings.moveCancelEnabled = (flags & 0x04u) != 0u;
            settings.lmbEnabled = (flags & 0x08u) != 0u;
            settings.holdDurationMs = static_cast<int>(r.u32());
            settings.moveCancelPixels = static_cast<int>(r.u32());
        }

        // Pick one of the three buttons from two otherwise-unused op bits (bits 3-4; bits 0-2
        // are the operation, bit 6 the settings-mutate flag).
        MouseButton button = MouseButton::Right;
        switch ((op >> 3) & 0x03u)
        {
        case 0:
            button = MouseButton::Left;
            break;
        case 1:
            button = MouseButton::Middle;
            break;
        default:
            button = MouseButton::Right;
            break;
        }
        const long x = static_cast<long>(static_cast<int32_t>(r.u32()));
        const long y = static_cast<long>(static_cast<int32_t>(r.u32()));
        const PointL pt{ x, y };

        switch (op & 0x07u)
        {
        case 0:
            (void)engine.OnButtonDown(button, tick, pt, settings);
            break;
        case 1:
            (void)engine.OnButtonUp(button, tick, settings);
            break;
        case 2:
            engine.OnMove(tick, pt, settings);
            break;
        case 3:
            engine.EnforceEnabled(settings);
            break;
        case 4:
            engine.ReleaseAll();
            break;
        case 5:
            engine.ResetTransient();
            break;
        case 6:
            (void)engine.IsLocked(button);
            break;
        default:
            // A full down/up pair at the same tick (covers exact-threshold and lock latching).
            (void)engine.OnButtonDown(button, tick, pt, settings);
            (void)engine.OnButtonUp(button, tick, settings);
            break;
        }
    }

    // Always drain any lock so the release/injection path runs at least once per input.
    engine.ReleaseAll();
    return 0;
}

#ifndef DISABLE_FOR_FUZZING

// Plain entry point for the non-fuzzing (Debug) configuration: runs one canned input so the
// project is runnable/debuggable without the libFuzzer driver.
int main()
{
    const uint8_t seed[] = {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
        11, 12, 13, 14, 15, 16, 17, 18, 19, 20
    };
    return LLVMFuzzerTestOneInput(seed, sizeof(seed));
}

#endif
