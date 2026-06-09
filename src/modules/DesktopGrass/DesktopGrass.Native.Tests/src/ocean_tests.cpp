// ocean_tests.cpp
//
// Ocean scene tests (architecture.md §17). Mirror of the Win2D OceanTests so
// the coral blade variant, bubble emitter, and fish swimmers stay in lockstep
// across impls.

#include "../third_party/catch2/catch.hpp"
#include "Sim.h"

#include <algorithm>

using namespace desktopgrass;

namespace {

constexpr double kMonitor1920 = 1920.0;

Sim make_ocean_sim(uint64_t seed = CANONICAL_TEST_SEED,
                   double width = kMonitor1920,
                   double density = DEFAULT_DENSITY) {
    Sim sim = sim_init(seed, width, density);
    sim_set_scene(sim, Scene::Ocean);
    return sim;
}

int count_kind(const Sim& sim, EntityKind kind) {
    return static_cast<int>(std::count_if(sim.entities.begin(), sim.entities.end(),
        [kind](const Entity& e) { return e.kind == kind; }));
}

} // namespace

TEST_CASE("Ocean scene generates at least one coral and keeps values in range",
          "[ocean][coral]") {
    Sim sim = make_ocean_sim();

    int coralCount = 0;
    for (const Blade& b : sim.blades) {
        if (!b.isCoral) continue;
        ++coralCount;
        REQUIRE_FALSE(b.isPine);
        REQUIRE_FALSE(b.isCactus);
        REQUIRE_FALSE(b.isMaple);
        REQUIRE_FALSE(b.isFlower);
        REQUIRE_FALSE(b.isMushroom);
        REQUIRE(b.coralHeight >= CORAL_HEIGHT_MIN);
        REQUIRE(b.coralHeight <= CORAL_HEIGHT_MAX);
        REQUIRE(b.coralWidth  >= CORAL_WIDTH_MIN);
        REQUIRE(b.coralWidth  <= CORAL_WIDTH_MAX);
        REQUIRE(static_cast<int>(b.coralType)     >= 0);
        REQUIRE(static_cast<int>(b.coralType)     <= CORAL_TYPE_COUNT  - 1);
        REQUIRE(static_cast<int>(b.coralColorIdx) >= 0);
        REQUIRE(static_cast<int>(b.coralColorIdx) <= CORAL_COLOR_COUNT - 1);
    }
    REQUIRE(coralCount > 0);
}

TEST_CASE("Ocean scene spawns initial fish at or above the target minimum",
          "[ocean][fish]") {
    Sim sim = make_ocean_sim();

    const int fishCount = count_kind(sim, EntityKind::Fish);
    REQUIRE(fishCount >= FISH_COUNT_MIN);
    REQUIRE(fishCount <= FISH_COUNT_MAX);
}

TEST_CASE("Ocean fish count rounds half-to-even deterministically",
          "[ocean][fish]") {
    // scaled = 2.5 * width / 1920. Widths chosen so scaled lands exactly on a
    // .5 tie; round-half-to-even must pick the even neighbor (NOT half-up),
    // matching C# Math.Round and independent of the FPU rounding mode.
    Sim tie25 = make_ocean_sim(CANONICAL_TEST_SEED, 1920.0); // scaled 2.5 -> 2
    REQUIRE(count_kind(tie25, EntityKind::Fish) == 2);

    Sim tie45 = make_ocean_sim(CANONICAL_TEST_SEED, 3456.0); // scaled 4.5 -> 4
    REQUIRE(count_kind(tie45, EntityKind::Fish) == 4);
}

TEST_CASE("Ocean tick emits bubbles over time", "[ocean][bubble]") {
    Sim sim = make_ocean_sim();

    const double dt = 1.0 / 60.0;
    for (int i = 0; i < 600; ++i) {
        sim.globalTime += dt;
        sim_tick_entities(sim, dt);
    }

    REQUIRE(count_kind(sim, EntityKind::Bubble) > 0);
}

TEST_CASE("Switching from Ocean to Grass wipes bubbles and fish",
          "[ocean][scene]") {
    Sim sim = make_ocean_sim();

    const double dt = 1.0 / 60.0;
    for (int i = 0; i < 120; ++i) {
        sim.globalTime += dt;
        sim_tick_entities(sim, dt);
    }
    REQUIRE(count_kind(sim, EntityKind::Fish) > 0);

    sim_set_scene(sim, Scene::Grass);

    REQUIRE(count_kind(sim, EntityKind::Bubble) == 0);
    REQUIRE(count_kind(sim, EntityKind::Fish) == 0);
    REQUIRE(std::none_of(sim.blades.begin(), sim.blades.end(),
        [](const Blade& b) { return b.isCoral; }));
}

TEST_CASE("Ocean palette is pinned in scene palettes", "[ocean][palette]") {
    for (int i = 0; i < PALETTE_SIZE; ++i) {
        REQUIRE(SCENE_PALETTES[static_cast<int>(Scene::Ocean)][i] == OCEAN_PALETTE[i]);
    }
}
