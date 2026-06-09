// gust_tests.cpp
//
// Gust impulse model tests (architecture.md §8).

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <cmath>

using namespace desktopgrass;

namespace {

Sim make_sim_with_blades(std::initializer_list<double> baseXs) {
    Sim sim;
    sim.windowHeight = STRIP_HEIGHT + HEADROOM;
    for (double x : baseXs) {
        Blade b{};
        b.baseX            = x;
        b.height           = 20.0;
        b.thickness        = 1.5;
        b.swayPhaseOffset  = 0.0;
        b.stiffness        = 1.0;
        b.cutHeight        = 1.0;
        b.cutInitialHeight = 1.0;
        b.cutAnimStart     = -1.0;
        sim.blades.push_back(b);
    }
    return sim;
}

InputEvent move(double x, double y, double t) {
    return InputEvent{ EventType::Move, x, y, t };
}

} // anonymous

TEST_CASE("first move event is a baseline; no impulse", "[gust]") {
    Sim sim = make_sim_with_blades({100.0});
    const double groundY = sim.windowHeight;
    const double bandY   = groundY - 10.0; // in band

    sim_apply_move(sim, move(100.0, bandY, 0.0));
    REQUIRE(sim.blades[0].gustVelocity == Approx(0.0));
    REQUIRE(sim.prevCursorTime == Approx(0.0));
}

TEST_CASE("a second move inside the band emits an impulse", "[gust]") {
    Sim sim = make_sim_with_blades({100.0, 200.0, 400.0});
    const double bandY = sim.windowHeight - 10.0;

    sim_apply_move(sim, move(  0.0, bandY, 0.0));
    sim_apply_move(sim, move(100.0, bandY, 0.05));  // velocity = 2000 DIP/sec

    // Blade at 100 is right under the cursor → max impulse.
    // Expected magnitude:
    //   capped = 2000 (≤ 4000 cap)
    //   impulseMagnitude = 2000 * 0.003 = 6.0
    //   smoothstep at distance 0 = 1.0
    //   smoothstep at distance 100/150 (blade @ 200) = (1-2/3)² * (3 - 2*(1-2/3))
    REQUIRE(sim.blades[0].gustVelocity == Approx(6.0).margin(1e-9));
    // Blade outside radius (400, dist=300 > 150) → no impulse.
    REQUIRE(sim.blades[2].gustVelocity == Approx(0.0));
    REQUIRE(sim.blades[1].gustVelocity > 0.0);
    REQUIRE(sim.blades[1].gustVelocity < 6.0);
}

TEST_CASE("impulse is signed by motion direction", "[gust]") {
    Sim left  = make_sim_with_blades({100.0});
    Sim right = make_sim_with_blades({100.0});

    const double y = left.windowHeight - 10.0;
    sim_apply_move(left,  move(200.0, y, 0.0));
    sim_apply_move(left,  move(100.0, y, 0.05)); // moving left

    sim_apply_move(right, move(  0.0, y, 0.0));
    sim_apply_move(right, move(100.0, y, 0.05)); // moving right

    REQUIRE(left.blades[0].gustVelocity  < 0.0);
    REQUIRE(right.blades[0].gustVelocity > 0.0);
    REQUIRE(std::fabs(left.blades[0].gustVelocity) ==
            Approx(std::fabs(right.blades[0].gustVelocity)).margin(1e-9));
}

TEST_CASE("cursor speed is capped at MAX_CURSOR_SPEED", "[gust]") {
    Sim sim = make_sim_with_blades({100.0});
    const double y = sim.windowHeight - 10.0;

    sim_apply_move(sim, move(0.0,      y, 0.0));
    sim_apply_move(sim, move(100000.0, y, 0.05)); // velocity ≈ 2e6 DIP/sec

    // capped magnitude = MAX_CURSOR_SPEED * IMPULSE_SCALE = 4000 * 0.003 = 12
    // but the blade is at distance ~100k from cursor: outside radius → no impulse
    REQUIRE(sim.blades[0].gustVelocity == Approx(0.0));
}

TEST_CASE("max impulse at the cursor equals capped magnitude", "[gust]") {
    Sim sim = make_sim_with_blades({1000.0});
    const double y = sim.windowHeight - 10.0;

    sim_apply_move(sim, move(0.0,    y, 0.0));
    sim_apply_move(sim, move(1000.0, y, 0.0001)); // velocity huge → saturates

    // Saturated: cursor lands at x=1000 (blade), distance=0, smoothstep=1.0
    REQUIRE(sim.blades[0].gustVelocity ==
            Approx(MAX_CURSOR_SPEED * IMPULSE_SCALE).margin(1e-9));
}

TEST_CASE("moves outside the gust band don't emit impulses", "[gust]") {
    Sim sim = make_sim_with_blades({100.0});
    const double y_above_band = sim.windowHeight - STRIP_HEIGHT - HEADROOM - 20.0;

    sim_apply_move(sim, move(  0.0, y_above_band, 0.0));
    sim_apply_move(sim, move(100.0, y_above_band, 0.05));
    REQUIRE(sim.blades[0].gustVelocity == Approx(0.0));
}

TEST_CASE("out-of-band move updates baseline; re-entry parity", "[gust]") {
    Sim sim = make_sim_with_blades({700.0});
    const double inBandY    = sim.windowHeight - 10.0;
    const double outOfBandY = sim.windowHeight - STRIP_HEIGHT - HEADROOM - 20.0;

    // t0 in-band: primes baseline (first event, no impulse).
    sim_apply_move(sim, move(500.0, inBandY, 0.0));
    // t1 out-of-band: updates baseline but emits no impulse.
    sim_apply_move(sim, move(520.0, outOfBandY, 0.05));
    REQUIRE(sim.prevCursorX    == Approx(520.0));
    REQUIRE(sim.prevCursorTime == Approx(0.05));
    REQUIRE(sim.blades[0].gustVelocity == Approx(0.0));

    // t2 re-enter in-band: emits impulse off the out-of-band baseline.
    sim_apply_move(sim, move(700.0, inBandY, 0.10));

    const double dtEv  = std::max(0.10 - 0.05, 1.0 / 1000.0);
    const double velX  = (700.0 - 520.0) / dtEv;
    const double capped = std::max(-MAX_CURSOR_SPEED, std::min(velX, MAX_CURSOR_SPEED));
    const double expected = capped * IMPULSE_SCALE; // distance 0 → smoothstep = 1
    REQUIRE(sim.blades[0].gustVelocity == Approx(expected).margin(1e-9));
}

TEST_CASE("large time gap resets cursor baseline", "[gust]") {
    Sim sim = make_sim_with_blades({100.0});
    const double y = sim.windowHeight - 10.0;

    sim_apply_move(sim, move(  0.0, y, 0.0));
    sim_apply_move(sim, move(500.0, y, 0.5));   // > CURSOR_REINIT_GAP_SEC (0.25)
    REQUIRE(sim.blades[0].gustVelocity == Approx(0.0));
}

TEST_CASE("impulse falls off smoothly with distance", "[gust]") {
    Sim sim = make_sim_with_blades({100.0, 130.0, 175.0, 200.0, 249.0, 251.0});
    const double y = sim.windowHeight - 10.0;

    sim_apply_move(sim, move(  0.0, y, 0.0));
    sim_apply_move(sim, move(100.0, y, 0.05));

    // Monotonic falloff: cursor at 100.
    REQUIRE(sim.blades[0].gustVelocity > sim.blades[1].gustVelocity);
    REQUIRE(sim.blades[1].gustVelocity > sim.blades[2].gustVelocity);
    REQUIRE(sim.blades[2].gustVelocity > sim.blades[3].gustVelocity);
    REQUIRE(sim.blades[3].gustVelocity > sim.blades[4].gustVelocity);
    // Just outside radius (251 → distance 151 > 150) → zero.
    REQUIRE(sim.blades[5].gustVelocity == Approx(0.0));
}
