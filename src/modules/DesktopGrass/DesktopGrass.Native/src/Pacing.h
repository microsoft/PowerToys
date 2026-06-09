// Pacing.h
//
// Frame pacing helper.
//
// The default Windows system timer resolution is ~15.6 ms, which clamps any
// `MsgWaitForMultipleObjectsEx(NULL, waitMs, ...)`-based loop to roughly
// 64 fps even when the caller asks for a shorter wait. At our 30 fps target
// that's actually below the requested cadence: a frame that asks for ~30 ms
// of wait ends up paying for two ~15.6 ms ticks (~31 ms) on the lucky path
// and three ticks (~46 ms) on the unlucky one, producing visibly uneven
// motion and dt_p95 around 48 ms.
//
// `FramePacer` uses a per-process high-resolution waitable timer
// (CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, Windows 10 1803+) so the wait
// honours its argument to roughly sub-ms granularity without changing the
// system-wide timer resolution. If the high-res timer cannot be created the
// pacer transparently falls back to the legacy ms-resolution wait, matching
// the pre-fix behaviour.

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

namespace desktopgrass {

class FramePacer {
public:
    FramePacer();
    ~FramePacer();

    FramePacer(const FramePacer&)            = delete;
    FramePacer& operator=(const FramePacer&) = delete;

    // True iff the high-resolution waitable timer was created. False means the
    // pacer is operating in legacy MWFMOe(NULL, waitMs, ...) mode.
    bool IsHighResolution() const { return timer_ != nullptr; }

    // Block until `waitSec` elapses or input arrives in the calling thread's
    // message queue (QS_ALLINPUT, MWMO_INPUTAVAILABLE). Returns immediately
    // when `waitSec <= 0`.
    void WaitUntilNextFrame(double waitSec);

private:
    HANDLE timer_ = nullptr;
};

} // namespace desktopgrass
