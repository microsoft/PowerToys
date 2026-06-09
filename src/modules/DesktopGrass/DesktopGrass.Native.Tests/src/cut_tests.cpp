// cut_tests.cpp
//
// Cut state animation tests (architecture.md §9).

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"
#include "snapshot_data.h"

#include <cmath>
#include <vector>

using namespace desktopgrass;
using namespace desktopgrass::test;

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

InputEvent click(double x, double y, double t) {
    return InputEvent{ EventType::Click, x, y, t };
}

} // anonymous

TEST_CASE("click inside cut band animates blades within radius to 0", "[cut]") {
    Sim sim = make_sim_with_blades({100.0, 110.0, 200.0});
    const double y_in_band = sim.windowHeight - 40.0; // inside strip

    InputEvent ev = click(100.0, y_in_band, 0.0);
    sim_tick(sim, 0.0, &ev, 1);

    // Apply 5 ticks of 50 ms (total = 250 ms > CUT_DURATION_SEC).
    for (int i = 0; i < 5; ++i) {
        sim_tick(sim, 0.05, nullptr, 0);
    }

    REQUIRE(sim.blades[0].cutHeight == Approx(0.0));
    REQUIRE(sim.blades[0].cutAnimStart == Approx(-1.0));
    REQUIRE(sim.blades[1].cutHeight == Approx(0.0));
    // Blade at 200 is outside CUT_RADIUS = 30.
    REQUIRE(sim.blades[2].cutHeight == Approx(1.0));
    REQUIRE(sim.blades[2].cutAnimStart == Approx(-1.0));
}

TEST_CASE("cut animation is linear over CUT_DURATION_SEC", "[cut]") {
    Sim sim = make_sim_with_blades({100.0});
    const double y = sim.windowHeight - 40.0;

    InputEvent ev = click(100.0, y, 0.0);
    sim_tick(sim, 0.0, &ev, 1);
    // After tick(0.0) globalTime = 0 still; cutAnimStart = 0.

    // 50 ms in → cutHeight ≈ 0.75.
    sim_tick(sim, 0.05, nullptr, 0);
    REQUIRE(sim.blades[0].cutHeight == Approx(0.75).margin(1e-9));

    // 100 ms in → 0.5.
    sim_tick(sim, 0.05, nullptr, 0);
    REQUIRE(sim.blades[0].cutHeight == Approx(0.5).margin(1e-9));

    // 150 ms in → 0.25.
    sim_tick(sim, 0.05, nullptr, 0);
    REQUIRE(sim.blades[0].cutHeight == Approx(0.25).margin(1e-9));

    // 200 ms in → 0.0 and idle.
    sim_tick(sim, 0.05, nullptr, 0);
    REQUIRE(sim.blades[0].cutHeight == Approx(0.0).margin(1e-9));
    REQUIRE(sim.blades[0].cutAnimStart < 0.0);
}

TEST_CASE("click outside cut band is ignored", "[cut]") {
    Sim sim = make_sim_with_blades({100.0});
    const double y_above = sim.windowHeight - STRIP_HEIGHT - 5.0;

    InputEvent ev = click(100.0, y_above, 0.0);
    sim_tick(sim, 0.0, &ev, 1);

    REQUIRE(sim.blades[0].cutHeight == Approx(1.0));
    REQUIRE(sim.blades[0].cutAnimStart < 0.0);
}

TEST_CASE("repeat click on in-flight blade is idempotent", "[cut]") {
    Sim sim = make_sim_with_blades({100.0});
    const double y = sim.windowHeight - 40.0;

    InputEvent first = click(100.0, y, 0.0);
    sim_tick(sim, 0.0, &first, 1);

    // Mid-animation second click → should not reset cutAnimStart.
    sim_tick(sim, 0.05, nullptr, 0);  // 0.05 elapsed; cutHeight = 0.75
    const double startSnapshot = sim.blades[0].cutAnimStart;
    const double heightSnapshot = sim.blades[0].cutHeight;

    InputEvent second = click(100.0, y, 0.05);
    sim_tick(sim, 0.0, &second, 1);

    REQUIRE(sim.blades[0].cutAnimStart    == Approx(startSnapshot));
    REQUIRE(sim.blades[0].cutInitialHeight == Approx(1.0));
    REQUIRE(sim.blades[0].cutHeight == Approx(heightSnapshot));
}

TEST_CASE("click on already-cut blade is a no-op", "[cut]") {
    Sim sim = make_sim_with_blades({100.0});
    sim.blades[0].cutHeight        = 0.0;
    sim.blades[0].cutInitialHeight = 0.0;
    sim.blades[0].cutAnimStart     = -1.0;

    const double y = sim.windowHeight - 40.0;
    InputEvent ev = click(100.0, y, 0.0);
    sim_tick(sim, 0.0, &ev, 1);

    REQUIRE(sim.blades[0].cutHeight    == Approx(0.0));
    REQUIRE(sim.blades[0].cutAnimStart  < 0.0);
}

TEST_CASE("blades outside cut radius are untouched", "[cut]") {
    Sim sim = make_sim_with_blades({100.0, 131.0, 200.0});
    const double y = sim.windowHeight - 40.0;

    InputEvent ev = click(100.0, y, 0.0);
    sim_tick(sim, 0.0, &ev, 1);

    REQUIRE(sim.blades[0].cutAnimStart >= 0.0);
    REQUIRE(sim.blades[1].cutAnimStart  < 0.0);
    REQUIRE(sim.blades[2].cutAnimStart  < 0.0);
}

TEST_CASE("compute_blade_stroke degenerates to a stump under threshold", "[cut][geometry]") {
    Blade b{};
    b.baseX            = 100.0;
    b.height           = 20.0;
    b.thickness        = 1.5;
    b.hue              = 2;
    b.cutHeight        = 0.04;       // below CUT_STUMP_THRESHOLD = 0.05
    b.effectiveLean    = 5.0;
    b.cutInitialHeight = 1.0;
    b.cutAnimStart     = 0.0;

    Stroke s = compute_blade_stroke(b, 110.0, Scene::Grass);
    REQUIRE(s.tip.x == Approx(100.0));
    REQUIRE(s.tip.y == Approx(110.0 - STUMP_HEIGHT));
    REQUIRE(s.argb  == PALETTE[2]);
}

TEST_CASE("compute_blade_stroke produces vertical line when lean is zero", "[cut][geometry]") {
    Blade b{};
    b.baseX         = 100.0;
    b.height        = 20.0;
    b.thickness     = 1.5;
    b.hue           = 1;
    b.cutHeight     = 1.0;
    b.effectiveLean = 0.0;

    Stroke s = compute_blade_stroke(b, 110.0, Scene::Grass);
    REQUIRE(s.base.x    == Approx(100.0));
    REQUIRE(s.base.y    == Approx(110.0));
    REQUIRE(s.tip.x     == Approx(100.0));
    REQUIRE(s.tip.y     == Approx(90.0));
    REQUIRE(s.control.x == Approx(100.0));
}

// ---------------------------------------------------------------------------
// Cut-floor (stubble) variation
// ---------------------------------------------------------------------------

TEST_CASE("generated blades get a per-blade cut floor within spec range", "[cut][floor]") {
    std::vector<Blade> blades;
    generate_blades(CANONICAL_TEST_SEED, 1920.0, 1.0, blades);
    REQUIRE(blades.size() > 50);

    for (const Blade& b : blades) {
        REQUIRE(b.cutFloor >= CUT_FLOOR_MIN);
        REQUIRE(b.cutFloor <  CUT_FLOOR_MAX);
        // Stubble must render as a short blade, never a degenerate stump.
        REQUIRE(b.cutFloor >= CUT_STUMP_THRESHOLD);
    }

    // The whole point is variation: not every blade settles at the same height.
    bool varies = false;
    for (std::size_t i = 1; i < blades.size(); ++i) {
        if (blades[i].cutFloor != blades[0].cutFloor) { varies = true; break; }
    }
    REQUIRE(varies);
}

TEST_CASE("cut settles at the per-blade stubble floor, not flat zero", "[cut][floor]") {
    Blade b{};
    b.height           = 20.0;
    b.thickness        = 1.5;
    b.cutHeight        = 1.0;
    b.cutInitialHeight = 1.0;
    b.cutFloor         = 0.12;
    b.cutAnimStart     = 0.0;

    // Advance past the full cut duration.
    advance_cut(b, CUT_DURATION_SEC + 0.01);

    REQUIRE(b.cutHeight    == Approx(0.12));
    REQUIRE(b.cutAnimStart == Approx(-1.0));
}

TEST_CASE("cut-down animation lerps toward the floor", "[cut][floor]") {
    Blade b{};
    b.height           = 20.0;
    b.cutHeight        = 1.0;
    b.cutInitialHeight = 1.0;
    b.cutFloor         = 0.10;
    b.cutAnimStart     = 0.0;

    // Half-way through the cut: lerp(1.0 -> 0.10) at t=0.5 = 0.10 + 0.90*0.5.
    advance_cut(b, CUT_DURATION_SEC * 0.5);
    REQUIRE(b.cutHeight == Approx(0.10 + 0.90 * 0.5).margin(1e-9));
}

TEST_CASE("regrowth grows back from the floor to full height", "[cut][floor][regrowth]") {
    Blade b{};
    b.height         = 20.0;
    b.cutFloor       = 0.10;
    b.cutHeight      = 0.10;
    b.cutAnimStart   = -1.0;
    b.regrowDuration = 0.4;
    b.regrowStart    = 0.0;

    // Half-way through regrowth: lerp(0.10 -> 1.0) at t=0.5.
    advance_cut(b, 0.2);
    REQUIRE(b.cutHeight == Approx(0.10 + 0.90 * 0.5).margin(1e-9));

    // Fully regrown.
    advance_cut(b, 0.4);
    REQUIRE(b.cutHeight == Approx(1.0).margin(1e-9));
}

TEST_CASE("zero-floor blades still collapse fully (back-compat)", "[cut][floor]") {
    Blade b{};
    b.height           = 20.0;
    b.cutHeight        = 1.0;
    b.cutInitialHeight = 1.0;
    b.cutFloor         = 0.0;
    b.cutAnimStart     = 0.0;

    advance_cut(b, CUT_DURATION_SEC + 0.01);
    REQUIRE(b.cutHeight == Approx(0.0));
}
