// Pacing.cpp

#include "Pacing.h"

namespace desktopgrass {

namespace {

// SetWaitableTimer's lpDueTime takes 100-ns intervals. Negative values mean
// "relative to now". One second == 10,000,000 hundred-ns units.
constexpr double kHundredNsPerSec = 10'000'000.0;

} // anonymous

FramePacer::FramePacer() {
    // CREATE_WAITABLE_TIMER_HIGH_RESOLUTION (0x00000002) requires Windows 10
    // 1803+. DesktopGrass already requires Windows 10 1809+ (see README), so
    // creation should succeed in supported environments. The nullptr returned
    // on any older system is fine: WaitUntilNextFrame falls back to the
    // legacy MWFMOe(NULL, waitMs, ...) path so behaviour degrades gracefully.
    timer_ = CreateWaitableTimerExW(
        nullptr, nullptr,
        CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
        TIMER_ALL_ACCESS);
}

FramePacer::~FramePacer() {
    if (timer_) {
        CloseHandle(timer_);
        timer_ = nullptr;
    }
}

void FramePacer::WaitUntilNextFrame(double waitSec) {
    if (waitSec <= 0.0) return;

    if (timer_) {
        // Relative due time in 100-ns units; round down so we never sleep
        // longer than asked. SetWaitableTimer will fire immediately if the
        // computed magnitude is zero.
        LARGE_INTEGER due{};
        const double hundredNs = waitSec * kHundredNsPerSec;
        due.QuadPart = -static_cast<LONGLONG>(hundredNs);

        if (SetWaitableTimer(timer_, &due, 0, nullptr, nullptr, FALSE)) {
            MsgWaitForMultipleObjectsEx(
                1, &timer_, INFINITE, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
            return;
        }
        // SetWaitableTimer can theoretically fail (e.g. handle revoked);
        // fall through to the legacy wait so the loop still makes progress.
    }

    // Legacy ms-resolution wait. Round to nearest millisecond.
    const DWORD waitMs = static_cast<DWORD>(waitSec * 1000.0 + 0.5);
    MsgWaitForMultipleObjectsEx(
        0, nullptr, waitMs, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
}

} // namespace desktopgrass
