// pacing_tests.cpp
//
// FramePacer behaviour tests.
//
// Goal: lock in the contract that on supported Windows (10 1803+) the pacer
// honours sub-15.6 ms waits via the high-resolution waitable timer, not the
// default system timer resolution. A regression that drops the high-res flag
// would silently re-introduce the ~48 ms dt_p95 pacing bug; the timing-bound
// assertion below catches that without needing benchmark numbers.
//
// The timing assertions are deliberately generous (we measure absolute upper
// bounds, not exact wait times) so CI runners with momentary scheduling
// hiccups don't flake. Even at the loosest bound the test still distinguishes
// high-res (~sub-ms) from default-resolution (~15.6 ms minimum tick) behaviour.

#include "../third_party/catch2/catch.hpp"
#include "Pacing.h"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

using namespace desktopgrass;

namespace {

double qpc_now_sec() {
    LARGE_INTEGER c{}, f{};
    QueryPerformanceCounter(&c);
    QueryPerformanceFrequency(&f);
    return static_cast<double>(c.QuadPart) / static_cast<double>(f.QuadPart);
}

} // namespace

TEST_CASE("FramePacer: creates a high-resolution waitable timer on supported Windows",
          "[pacing]") {
    FramePacer pacer;
    // DesktopGrass requires Windows 10 1809+, which is well past the
    // CREATE_WAITABLE_TIMER_HIGH_RESOLUTION minimum (Win 10 1803). Build/CI
    // environments below that floor are not supported.
    REQUIRE(pacer.IsHighResolution());
}

TEST_CASE("FramePacer: zero or negative wait returns essentially immediately",
          "[pacing]") {
    FramePacer pacer;
    const double t0 = qpc_now_sec();
    pacer.WaitUntilNextFrame(0.0);
    pacer.WaitUntilNextFrame(-1.0);
    const double dt = qpc_now_sec() - t0;
    // Two no-op calls should complete in well under a millisecond, but allow
    // 5 ms of slop for loaded CI machines.
    REQUIRE(dt < 0.005);
}

TEST_CASE("FramePacer: honours sub-15.6 ms waits via the high-resolution timer",
          "[pacing]") {
    FramePacer pacer;
    REQUIRE(pacer.IsHighResolution());

    // Five 1 ms waits. With the high-resolution timer the cumulative time
    // should sit well below 30 ms. Without it (legacy ~15.6 ms tick) each
    // wait would round up to ~15.6 ms for a total of ~78 ms, so 30 ms is a
    // wide safety margin that still catches regressions cleanly.
    constexpr int    kIterations = 5;
    constexpr double kWaitSec    = 0.001;

    const double t0 = qpc_now_sec();
    for (int i = 0; i < kIterations; ++i) {
        pacer.WaitUntilNextFrame(kWaitSec);
    }
    const double total = qpc_now_sec() - t0;

    // Lower bound: we asked for 5 ms total — actual wait must be at least
    // a small fraction of that, otherwise we are not waiting at all.
    REQUIRE(total >= 0.0005);
    // Upper bound: must beat the default ~15.6 ms tick by a comfortable
    // margin. 30 ms catches the regression (78 ms) without flaking on CI.
    REQUIRE(total < 0.030);
}
